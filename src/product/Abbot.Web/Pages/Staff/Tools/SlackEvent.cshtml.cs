using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Pages.Staff.Tools;

public class SlackEventPage : StaffToolsPage
{
    static readonly ILogger<SlackEventPage> Log = ApplicationLoggerFactory.CreateLogger<SlackEventPage>();

    public DomId UnencryptedDomId => new("unencrypted-content");

    readonly AbbotContext _db;
    readonly IAuditLog _auditLog;

    public SlackEventPage(AbbotContext db, IAuditLog auditLog)
    {
        _db = db;
        _auditLog = auditLog;
    }

    [BindProperty]
    public InputModel Input { get; init; } = new();

    public SlackEvent? Details { get; private set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var slackEvent = await _db.SlackEvents.SingleOrDefaultAsync(e => e.EventId == id);
        if (slackEvent is null)
        {
            StatusMessage = $"Slack event not found for EventId {id}.";
            return RedirectToPage("Index");
        }

        Details = slackEvent;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        // The only reason to post here is to request access
        var slackEvent = await _db.SlackEvents.SingleOrDefaultAsync(e => e.EventId == id);
        if (slackEvent is null)
        {
            StatusMessage = $"Slack event not found for EventId {id}.";
            return RedirectToPage("Index");
        }

        if (!ModelState.IsValid)
        {
            Details = slackEvent;
            return Page();
        }

        var unencrypted = new UnencryptedEvent(FormatJson(slackEvent.Content.Reveal()), Input.Reason);
        Log.StaffSlackEventAccess(slackEvent.EventId, Input.Reason, Viewer.User.DisplayName, Viewer.User.Id);
        await _auditLog.LogStaffViewedSlackEventAsync(slackEvent, Input.Reason, Viewer.User);
        return TurboUpdate(UnencryptedDomId, Partial("_UnencryptedSlackEvent", unencrypted));
    }


    public record UnencryptedEvent(string Content, string Reason);

    static string FormatJson(string content)
    {
        if (content is { Length: 0 } || content[0] is not '{' or '[')
        {
            return content;
        }

        try
        {
            var deserialized = JsonConvert.DeserializeObject(content);
            return JsonConvert.SerializeObject(deserialized, Formatting.Indented);
        }
        catch (Exception)
        {
            return content;
        }
    }

    public class InputModel
    {
        public string Reason { get; set; } = null!;
    }
}
