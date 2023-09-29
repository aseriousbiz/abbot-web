using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Telemetry;

public enum StatusFilter
{
    [Display(Name = "All Statuses")]
    All,
    [Display(Name = "Success")]
    Success,
    [Display(Name = "Error")]
    Error
}
