using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages.Staff.Users;

public class DeleteMember : StaffToolsPage
{
    readonly AbbotContext _db;

    public DeleteMember(AbbotContext db)
    {
        _db = db;
    }

    public Member Subject { get; set; } = null!;

    public bool StartedConversations { get; private set; }

    public bool ParticipatedInConversations { get; private set; }

    public bool HasFacts { get; private set; }

    public bool HasRoles { get; private set; }

    public bool SlackTeamIdMatchesOrganizationPlatformId { get; private set; }

    public bool HasRoomAssignments { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var subject = await InitializeAsync(id);
        if (subject is null)
        {
            return NotFound();
        }

        Subject = subject;

        return Page();
    }

    async Task<Member?> InitializeAsync(int id)
    {
        var subject = await _db.Members
            .Include(m => m.Organization)
            .Include(m => m.User)
            .Include(m => m.Facts)
            .Include(m => m.MemberRoles)
            .Include(m => m.RoomAssignments)
            .SingleOrDefaultAsync(m => m.Id == id);

        if (subject is null)
        {
            return null;
        }

        SlackTeamIdMatchesOrganizationPlatformId = subject.User.SlackTeamId == subject.Organization.PlatformId;
        HasFacts = subject.Facts.Any();
        HasRoles = subject.MemberRoles.Any();
        HasRoomAssignments = subject.RoomAssignments.Any();
        StartedConversations = await _db.Conversations.Where(c => c.StartedById == id).AnyAsync();
        ParticipatedInConversations = await _db.ConversationMembers.Where(c => c.MemberId == id).AnyAsync();
        return subject;
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var subject = await InitializeAsync(id);

        if (subject is null)
        {
            return NotFound();
        }

        if (HasFacts || HasRoles || HasRoomAssignments || StartedConversations || ParticipatedInConversations)
        {
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Cannot delete this member due to FK relationships";
            return RedirectToPage();
        }

        _db.Members.Remove(subject);
        await _db.SaveChangesAsync();

        StatusMessage = "Member deleted";
        return RedirectToPage("Index");
    }
}
