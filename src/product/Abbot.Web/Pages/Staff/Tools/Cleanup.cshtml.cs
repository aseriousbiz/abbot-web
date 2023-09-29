using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Collections;

namespace Serious.Abbot.Pages.Staff.Tools;

public class CleanupPage : StaffToolsPage
{
    readonly AbbotContext _db;

    public CleanupPage(AbbotContext db)
    {
        _db = db;
    }

    public IPaginatedList<Member> Members { get; private set; } = null!;

    public IReadOnlyList<TagNameCount> DuplicateTags { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        // Get all members that are erroneously created.

        var query = from tag in _db.Tags
                    group tag by new { tag.OrganizationId, Name = tag.Name.ToUpper() }
            into g
                    where g.Count() > 1
                    select new { g.Key, Count = g.Count() };
        var duplicateConversationTags = await query.ToListAsync();
        var organizationIds = duplicateConversationTags.Select(g => g.Key.OrganizationId).ToArray();
        var orgLookup = await _db.Organizations.Where(o => organizationIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id);
        DuplicateTags = duplicateConversationTags
            .Select(t => new TagNameCount(orgLookup[t.Key.OrganizationId].Name ?? "unknown", t.Key.OrganizationId, t.Key.Name, t.Count))
            .ToList();

        var members = await _db.Members
            .Include(m => m.Facts)
            .Include(m => m.MemberRoles)
            .Include(m => m.User)
            .Include(m => m.Organization)
            .Where(m => m.User.SlackTeamId!.StartsWith("E"))
            .Where(m => m.User.SlackTeamId != m.Organization.PlatformId)
            .Where(m => m.User.SlackTeamId != m.Organization.EnterpriseGridId)
            .Where(m => m.Organization.PlatformType == PlatformType.Slack)
            .Where(m => !m.User.IsBot)
            .OrderBy(m => m.Organization.Id)
            .ToListAsync();

        Members = new PaginatedList<Member>(members, members.Count, 1, members.Count);
    }

    [BindProperty]
    public int OrganizationId { get; set; }

    [BindProperty]
    public string TagNameToFix { get; set; } = null!;

    public async Task<IActionResult> OnPostAsync()
    {
        var tags = await _db.Tags
            .Include(t => t.Conversations)
            .Where(t => t.OrganizationId == OrganizationId)
            .Where(t => t.Name.ToUpper() == TagNameToFix)
            .ToListAsync();

        if (tags.Count > 1)
        {
            try
            {
                await FixConversationTagsAsync(tags);
                StatusMessage = $"Fixed tag {TagNameToFix}";
            }
            catch (Exception e)
            {
                StatusMessage = $"{WebConstants.ErrorStatusPrefix}Error fixing tag {TagNameToFix}: {e.Message}";
            }
        }

        return RedirectToPage();
    }

    async Task FixConversationTagsAsync(IReadOnlyList<Tag> tags)
    {
        // First tag wins.
        var original = tags[0];

        // Move all conversations to the original tag.
        foreach (var dupeTag in tags.Skip(1))
        {
            // First remove the dupe tag from all conversations.
            var conversationTags = dupeTag.Conversations.ToList();
            foreach (var ct in conversationTags)
            {
                dupeTag.Conversations.Remove(ct);

                // Reload the conversation to make sure we have the latest tags.
                var conversation = await _db.Conversations
                    .Include(c => c.Tags)
                    .ThenInclude(t => t.Tag)
                    .Where(o => o.Id == ct.ConversationId)
                    .SingleOrDefaultAsync();

                // Make sure the conversation is tagged with the original tag.
                if (conversation is not null)
                {
                    if (conversation.Tags.All(t => t.TagId != original.Id))
                    {
                        conversation.Tags.Add(new ConversationTag
                        {
                            ConversationId = conversation.Id,
                            TagId = original.Id,
                            Created = ct.Created,
                            CreatorId = ct.CreatorId,
                        });
                    }
                }
                await _db.SaveChangesAsync();
            }

            // Finally, remove the dupe tag.
            _db.Tags.Remove(dupeTag);
            await _db.SaveChangesAsync();
        }
    }

    public record DupeTag(int ConversationId, DateTime Created, int CreatorId);

    public record TagNameCount(string OrganizationName, int OrganizationId, string Name, int Count);
}
