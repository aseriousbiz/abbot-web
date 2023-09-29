using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;

namespace Serious.Abbot.Controllers.PublicApi;

[ApiController]
[AbbotApiHost]
[Route("api/export")]
[Authorize(Policy = AuthorizationPolicies.PublicApi)]
public class ExportController : UserControllerBase
{
    readonly AbbotContext _db;

    public ExportController(AbbotContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Route("insights")]
    public async Task<IActionResult> GetInsightsAsync()
    {
        var conversations = (await _db.Conversations
            .Include(c => c.Room)
            .Include(c => c.Hub)
                .ThenInclude(h => h!.Room)
            .Include(c => c.StartedBy.User)
            .Include(c => c.Events)
                .ThenInclude(e => e.Member.User)
            .Include(c => c.Members)
                .ThenInclude(cm => cm.Member.User)
            .Include(c => c.Tags)
                .ThenInclude(ct => ct.Tag)
            .Include(c => c.Assignees).ThenInclude(a => a.User)
            .Where(c => c.OrganizationId == Organization.Id)
            .OrderBy(c => c.Created)
            .AsSplitQuery()
            .ToListAsync())
            .Select(c => new {
                id = c.Id,
                first_message_id = c.FirstMessageId,
                first_response_on = c.FirstResponseOn,
                tags = c.Tags.Select(t => new { name = t.Tag.Name }),
                assignees = c.Assignees.Select(GetMemberProjection),
                members = c.Members.Select(GetConversationMemberProjection),
                summary = c.Summary,
                title = c.Title,
                room = new { id = c.Room.PlatformRoomId, name = c.Room.Name },
                state = c.State,
                created = c.Created,
                last_message_posted_on = c.LastMessagePostedOn,
                last_state_changed = c.LastStateChangeOn,
                started_by = GetMemberProjection(c.StartedBy),
                closed_on = c.ClosedOn,
                archived_on = c.ArchivedOn,
                hub = c.Hub is { } hub ? new { id = hub.Room.PlatformRoomId, name = hub.Name } : null,
                events = c.Events
                    .OrderBy(e => e.Created)
                    .OfTypes<MessagePostedEvent, StateChangedEvent>(GetMessagePostedEventProjection, GetStateChangedEvent)

            });

        return Json(conversations);
    }

    static object GetConversationMemberProjection(ConversationMember conversationMember) => new {
        joined = conversationMember.JoinedConversationAt,
        last_posted_at = conversationMember.LastPostedAt,
        member = GetMemberProjection(conversationMember.Member),
    };

    static object GetMemberProjection(Member member) => new {
        id = member.User.PlatformUserId,
        name = member.DisplayName
    };

    static object GetMessagePostedEventProjection(MessagePostedEvent e)
    {
        var metadata = e.DeserializeMetadata();
        return new {
            id = e.Id,
            type = "message_posted",
            created = e.Created,
            posted_by = GetMemberProjection(e.Member),
            message_id = e.MessageId,
            message_url = e.MessageUrl,
            external_message_id = e.ExternalMessageId,
            external_author = new { id = e.ExternalAuthorId, name = e.ExternalAuthor },
            external_source = e.ExternalSource,
            categories = (metadata?.Categories ?? Array.Empty<Category>()).Select(c => new {
                name = c.Name,
                value = c.Value
            })
        };
    }

    static object GetStateChangedEvent(StateChangedEvent e)
    {
        return new {
            id = e.Id,
            type = "state_changed",
            created = e.Created,
            posted_by = GetMemberProjection(e.Member),
            message_id = e.MessageId,
            message_url = e.MessageUrl,
            old_state = e.OldState,
            new_state = e.NewState,
            @implicit = e.Implicit
        };
    }
}
