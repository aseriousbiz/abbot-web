using System.Collections.Generic;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Pages.Activity;

public class DetailsModel : UserPage
{
    static readonly ILogger<DetailsModel> Log = ApplicationLoggerFactory.CreateLogger<DetailsModel>();

    readonly IAuditLogReader _auditLogReader;
    readonly IAuditLog _auditLog;
    readonly IOrganizationRepository _organizationRepository;
    readonly AbbotContext _db;

    public DetailsModel(
        IAuditLogReader auditLogReader,
        IAuditLog auditLog,
        IOrganizationRepository organizationRepository,
        AbbotContext db)
    {
        _auditLogReader = auditLogReader;
        _auditLog = auditLog;
        _organizationRepository = organizationRepository;
        _db = db;
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Activity/Details", new { Id = AuditEvent.Identifier });

    /// <summary>
    /// If true, we'll show the properties of the event to non-staff. This is only for selected events.
    /// </summary>
    public bool ShowProperties { get; private set; }

    public AuditEventBase AuditEvent { get; private set; } = null!;

    public Member? Abbot { get; private set; }

    public Skill? Skill { get; private set; }

    public Conversation? Conversation { get; private set; }

    public Package? Package { get; private set; }

    public SkillTrigger? Trigger { get; private set; }

    public bool MaySeeCode { get; private set; }

    public bool StaffMaySeeCode { get; set; }

    public bool DetailsAvailable { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool CurrentUserIsAdmin { get; private set; }

    public bool CurrentUserIsStaff { get; private set; }

    /// <summary>
    /// If this is a code edit session, this is the final diff of the edit session.
    /// </summary>
    public DiffPaneModel? SessionDiff { get; private set; }

    /// <summary>
    /// All the related activity for this event as part of the same trace Id.
    /// </summary>
    public SkillChainModel? SignalChain { get; private set; }

    public AuditEventBase? ParentEvent { get; set; }

    public IReadOnlyList<AuditEventBase> RelatedEvents { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var (auditEvent, user) = await InitializePageAsync(id);
        if (auditEvent is null || user is null)
        {
            return NotFound();
        }

        AuditEvent = auditEvent;

        if (auditEvent.ParentIdentifier is not null)
        {
            ParentEvent = await _auditLogReader.GetAuditEntryAsync(auditEvent.ParentIdentifier.Value);
        }

        // Find any child and related events
        RelatedEvents = await _auditLogReader.GetRelatedEventsAsync(auditEvent);

        ViewData["ViewingAuditEvent"] = AuditEvent;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!HttpContext.IsStaffMode())
        {
            return NotFound();
        }

        var (auditEvent, staffUser) = await InitializePageAsync(id);
        if (auditEvent is null || staffUser is null)
        {
            return NotFound();
        }

        if (auditEvent is not SkillRunAuditEvent && auditEvent is not SkillEditSessionAuditEvent)
        {
            return NotFound();
        }

        AuditEvent = auditEvent;

        // Find any child and related events
        RelatedEvents = await _auditLogReader.GetRelatedEventsAsync(auditEvent);

        if (!ModelState.IsValid)
        {
            return Page();
        }
        StaffMaySeeCode = true;

        Log.StaffUserAccess(auditEvent.Identifier, Input.Reason, staffUser.DisplayName, staffUser.Id);

        await _auditLog.LogStaffViewedCodeEventAsync(
            auditEvent,
            Input.Reason,
            staffUser);
        return Page();
    }

    protected virtual async Task<(AuditEventBase? AuditEvent, User? User)> InitializePageAsync(Guid id)
    {
        CurrentUserIsAdmin = Viewer.IsAdministrator();
        CurrentUserIsStaff = Viewer.IsStaff();
        var auditEvent = await _auditLogReader.GetAuditEntryAsync(id);

        if (auditEvent is null || Organization.Id != auditEvent.OrganizationId && !HttpContext.IsStaffMode())
        {
            return (null, null);
        }

        ShowProperties = auditEvent.ShouldShowProperties();

        Abbot = await _organizationRepository.EnsureAbbotMember(auditEvent.Organization);

        MaySeeCode = User.IsAdministrator() && Viewer.OrganizationId == auditEvent.OrganizationId
                     || Viewer.UserId == auditEvent.ActorId;
        DetailsAvailable = HttpContext.IsStaffMode() || MaySeeCode;

        if (auditEvent is SkillAuditEvent skillAuditEvent)
        {
            Skill = await _db.Skills.FindAsync(skillAuditEvent.SkillId);
        }

        if (auditEvent is SkillRunAuditEvent { TraceId: { } } runEvent)
        {
            var relatedSkillRuns = await _auditLogReader
                .GetRelatedEventsAsync(runEvent);
            SignalChain = new SkillChainModel(auditEvent.Identifier, relatedSkillRuns);
        }

        switch (auditEvent)
        {
            case PackageEvent packageEvent:
                Package = await _db.Packages
                    .Include(p => p.Organization)
                    .Include(p => p.Skill)
                    .FirstOrDefaultAsync(p => p.Id == packageEvent.EntityId);
                break;

            case ConversationTitleChangedEvent:
                Conversation = await _db.Conversations.FindAsync(auditEvent.EntityId);
                break;

            case SkillEditSessionAuditEvent editSession:
                var initial = await _db.SkillVersions.FindAsync(editSession.FirstSkillVersionId);
                if (initial is not null) // We never delete skill versions, but just in case.
                {
                    SessionDiff = InlineDiffBuilder.Diff(initial.Code ?? string.Empty,
                        editSession.Code ?? string.Empty);
                }

                break;

            case TriggerChangeEvent:
            case TriggerRunEvent:
                Trigger = await _db.SkillTriggers
                    .Include(t => t.Creator)
                    .FirstOrDefaultAsync(t => t.Id == auditEvent.EntityId);
                break;
        }

        return (auditEvent, Viewer.User);
    }

    public class InputModel
    {
        public string Reason { get; set; } = null!;
    }

    public string Humanize(TimeSpan? timeSpan, string textIfNull = "not set")
    {
        return timeSpan?.Humanize() ?? textIfNull;
    }
}
