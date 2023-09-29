using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Telemetry;

public enum ActivityTypeFilter
{
    [Display(Name = "All user events")]
    User = 1,

    [Display(Name = "All events")]
    All = 0,

    [Display(Name = "Skill Run")]
    SkillRun = 2,

    [Display(Name = "Built-In Skill Run")]
    BuiltInSkillRun = 3,

    [Display(Name = "Skill Change")]
    SkillChange = 4,

    [Display(Name = "Skill Rename")]
    SkillRename = 5,

    [Display(Name = "Skill Code Edit")]
    SkillCodeEdit = 6,

    [Display(Name = "Skill Not Found")]
    SkillNotFound = 7,

    [Display(Name = "Installation")]
    Installation = 8,

    [Display(Name = "Permission Change")]
    Permission = 9,

    [Display(Name = "Package Changes")]
    Package = 10,

    [Display(Name = "Secret creation or deletion")]
    Secret = 11,

    [Display(Name = "Trigger creation or deletion")]
    Trigger = 12,

    [Display(Name = "Subscription changes")]
    Subscription = 13,

    [Display(Name = "Staff")]
    Staff = 14,

    [Display(Name = "Admin settings")]
    Admin = 15,
}

#pragma warning disable CA1027
public enum SkillEventFilter
#pragma warning restore CA1027
{
    [Display(Name = "All user events")]
    User = 1,

    [Display(Name = "All event types")]
    All = 0,

    [Display(Name = "Skill Run")]
    SkillRun = 2,

    [Display(Name = "Skill Change")]
    SkillChange = 4,

    [Display(Name = "Skill Rename")]
    SkillRename = 5,

    [Display(Name = "Skill Code Edit")]
    SkillCodeEdit = 6
}
