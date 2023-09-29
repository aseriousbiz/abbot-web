using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Skills;
using Serious.AspNetCore;

namespace Serious.Abbot.Models;

public class TriggerViewModel
{
    static readonly string? StandaloneTriggerHost =
        AllowedHosts.Trigger.Except(AllowedHosts.Web).FirstOrDefault();

    readonly SkillTrigger _trigger;

    public TriggerViewModel(SkillFeatureEditPageModel pageModel, SkillTrigger trigger, bool isDeleting = false, bool isReadonly = false, bool isEditing = false)
    {
        PageModel = pageModel;
        _trigger = trigger;
        IsDeleting = isDeleting;
        IsReadOnly = isReadonly;
        IsEditing = isEditing;
        CronSchedule = null!;
        CronScheduleDescription = null!;

        switch (_trigger)
        {
            case SkillHttpTrigger httpTrigger:
                ApiToken = httpTrigger.ApiToken;
                IsHttpTrigger = true;
                break;
            case SkillScheduledTrigger scheduledTrigger:
                HasArgs = scheduledTrigger.Arguments?.Length > 0;
                Arguments = scheduledTrigger.Arguments;
                IsScheduledTrigger = true;
                CronSchedule = scheduledTrigger.CronSchedule;
                CronScheduleDescription = scheduledTrigger.CronScheduleDescription;
                TimeZoneId = scheduledTrigger.TimeZoneId;
                break;
            default:
                throw new InvalidOperationException($"Unknown trigger type {trigger.GetType().Name}");
        }

        TriggerTypeRouteParam = _trigger.GetTriggerTypeRouteParameter();
    }

    public SkillFeatureEditPageModel PageModel { get; }

    public bool HasArgs { get; }

    [Display(Description = "The text after the skill name to pass to the skill.")]
    public string? Arguments { get; }

    public bool IsHttpTrigger { get; }
    public bool IsScheduledTrigger { get; }

    public string Name => _trigger.Name;
    [Display(Description = "Something to help remember what the trigger is for.")]
    public string? Description => _trigger.Description;
    public string? ApiToken { get; }
    [Display(Name = "Cron Schedule")]
    public string CronSchedule { get; }
    public string CronScheduleDescription { get; }
    public DateTime Created => _trigger.Created;
    public string CreatorName => _trigger.Creator.DisplayName;
    public string SkillName => _trigger.Skill.Name;
    [Display(Name = "Time Zone")]
    public string? TimeZoneId { get; }
    public bool IsDeleting { get; }
    public bool IsReadOnly { get; }
    public bool IsEditing { get; }

    public string TriggerTypeRouteParam { get; }

    // For localhost we use the /api/internal/skills/{skill}/trigger/{trigger}, because it runs on the same host
    // For production we use the {TriggerHostName}/{skill}/trigger/{trigger} because it's on its own host name.
    public Uri GetTriggerUrl(HttpRequest request)
    {
        return StandaloneTriggerHost is null
            ? request.GetFullyQualifiedUrl($"/api/internal/skills/{SkillName}/trigger/")
            : request.GetFullyQualifiedUrl(StandaloneTriggerHost, $"/{SkillName}/trigger/");
    }
}
