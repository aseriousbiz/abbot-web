using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Serious.Collections;
using Serious.Filters;

namespace Serious.Abbot.Pages.Settings.Account;

public class IndexPage : UserPage
{
    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly ILinkedIdentityRepository _linkedIdentityRepository;

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Users/Details", new { Id = Viewer.User.PlatformUserId });

    [BindProperty(Name = "q", SupportsGet = true)]
    public FilterList Filter { get; set; } = new();

    [BindProperty]
    public string? WorkingHoursStart { get; set; }

    [BindProperty]
    public string? WorkingHoursEnd { get; set; }

    [BindProperty]
    public WorkingDays WorkingDays { get; set; } = WorkingDays.Default;

    [BindProperty]
    public NotificationSettings Input { get; set; } = new(false, false);

    public bool ZendeskEnabled { get; private set; }

    public ZendeskIdentityInfo? ZendeskIdentity { get; private set; }

    public IPaginatedList<RoomViewModel> Rooms { get; set; } = null!;

    public IndexPage(
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        IIntegrationRepository integrationRepository,
        ILinkedIdentityRepository linkedIdentityRepository)
    {
        _userRepository = userRepository;
        _roomRepository = roomRepository;
        _integrationRepository = integrationRepository;
        _linkedIdentityRepository = linkedIdentityRepository;
    }

    public async Task OnGetAsync(int p = 1)
    {
        Input = Viewer.Properties.Notifications;

        WorkingHoursStart = (Viewer.WorkingHours?.Start ?? WorkingHours.Default.Start)
            .ToString("HH:mm", CultureInfo.InvariantCulture);

        WorkingHoursEnd = (Viewer.WorkingHours?.End ?? WorkingHours.Default.End)
            .ToString("HH:mm", CultureInfo.InvariantCulture);

        WorkingDays = Viewer.Properties.WorkingDays;

        var rooms = await _roomRepository.GetConversationRoomsAsync(
            Organization,
            Filter,
            p,
            WebConstants.ShortPageSize);

        Rooms = rooms.Map(r => new RoomViewModel(
            r,
            r.Assignments.Any(a => a.MemberId == Viewer.Id && a.Role == RoomRole.FirstResponder),
            r.Assignments.Any(a => a.MemberId == Viewer.Id && a.Role == RoomRole.EscalationResponder)));

        var zendeskIntegration = await _integrationRepository.GetIntegrationAsync(Organization, IntegrationType.Zendesk);
        if (zendeskIntegration is { Enabled: true })
        {
            ZendeskEnabled = true;
            var (linkedIdentity, _) = await _linkedIdentityRepository.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                Organization,
                Viewer,
                LinkedIdentityType.Zendesk);

            if (linkedIdentity is not null)
            {
                ZendeskIdentity = ZendeskIdentityInfo.FromLinkedIdentity(linkedIdentity);
            }
        }
    }

    public async Task<IActionResult> OnPostAsync(string? filter, int p)
    {
        // This will not happen in our UI, but we can't trust the client
        if (WorkingHoursStart is null || WorkingHoursEnd is null)
        {
            StatusMessage = "Both start and end working hours must be specified.";
            return RedirectToPage(new { filter, p });
        }

        // Again, we can't trust those sneaky clients.
        if (!TimeOnly.TryParseExact(WorkingHoursStart, "HH:mm", out var workingHoursStart) ||
            !TimeOnly.TryParseExact(WorkingHoursEnd, "HH:mm", out var workingHoursEnd))
        {
            StatusMessage = "Working hours must be specified as 'HH:mm', in 24-hour time.";
            return RedirectToPage(new { filter, p });
        }

        // Clients!!!!!!
        if (workingHoursStart.Minute is not (0 or 30) || workingHoursEnd.Minute is not (0 or 30))
        {
            StatusMessage = "Working hours must be specified on the hour or half-hour.";
            return RedirectToPage(new { filter, p });
        }

        await _userRepository.UpdateWorkingHoursAsync(
            Viewer,
            new WorkingHours(workingHoursStart, workingHoursEnd),
            WorkingDays);
        StatusMessage = "Working hours updated.";
        return RedirectToPage(new { filter, p });
    }

    public async Task<IActionResult> OnPostNotificationSettingsAsync(string? filter, int p)
    {
        Viewer.Properties = new MemberProperties(Input);
        await _userRepository.UpdateUserAsync();
        StatusMessage = "Notification settings updated.";
        return RedirectToPage(new { filter, p });
    }

    public record RoomViewModel(Room Room, bool IsFirstResponder, bool IsEscalationResponder);
}

public record ZendeskIdentityInfo(string Name, long Id)
{
    public static ZendeskIdentityInfo FromLinkedIdentity(LinkedIdentity linkedIdentity)
    {
        var zendeskLink = ZendeskUserLink.Parse(linkedIdentity.ExternalId).Require();
        return new ZendeskIdentityInfo(linkedIdentity.ExternalName ?? "(Unknown Name)", zendeskLink.UserId);
    }
}
