using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.Abbot.Entities;
using Serious.AspNetCore.DataAnnotations;

namespace Serious.Abbot.Pages.Settings.Organization;

public class ResponseTimeSettings
{
    public static readonly IReadOnlyList<SelectListItem> Units = new List<SelectListItem>()
    {
        new("days", TimeUnits.Days.ToString()),
        new("hours", TimeUnits.Hours.ToString()),
        new("minutes", TimeUnits.Minutes.ToString()),
    };

    public TimeUnits TargetUnits { get; init; }

    [Display(Name = "Target Response Time")]
    [RequiredIf(nameof(UseCustomResponseTimes), true)]
    [Range(0, 999, ErrorMessage = "Value must be between 0 and 999")]
    public int? TargetValue { get; init; }

    [ValidateNever]
    public bool IsTargetRounded { get; init; }

    public TimeUnits DeadlineUnits { get; init; }

    [Display(Name = "Deadline Response Time")]
    [RequiredIf(nameof(UseCustomResponseTimes), true)]
    [Range(0, 999, ErrorMessage = "Value must be between 0 and 999")]
    public int? DeadlineValue { get; init; }

    [ValidateNever]
    public bool IsDeadlineRounded { get; init; }

    [ValidateNever]
    public bool ReadOnly { get; init; }

    [ValidateNever]
    public bool UseCustomResponseTimes { get; init; }

    public static ResponseTimeSettings FromTimeToRespond(
        Threshold<TimeSpan> timeToRespond,
        bool readOnly,
        bool useCustomResponseTimes)
    {
        static (int? Value, TimeUnits Units, bool IsRounded) GetValueAndUnits(TimeSpan? span)
        {
            if (span is { } val)
            {
                if (val == TimeSpan.Zero)
                {
                    return (null, TimeUnits.Minutes, false);
                }

                if (val.TotalMinutes % (24 * 60) == 0)
                {
                    return ((int)(val.TotalMinutes / (24 * 60)), TimeUnits.Days, false);
                }

                if (val.TotalMinutes % 60 == 0)
                {
                    return ((int)(val.TotalMinutes / 60), TimeUnits.Hours, false);
                }

                var rounded = (int)Math.Ceiling(val.TotalMinutes);
                return (rounded, TimeUnits.Minutes, rounded != val.TotalMinutes);
            }

            return (null, TimeUnits.Minutes, false);
        }

        var (targetValue, targetUnits, isTargetRounded) = GetValueAndUnits(timeToRespond.Warning);
        var (deadlineValue, deadlineUnits, isDeadlineRounded) = GetValueAndUnits(timeToRespond.Deadline);

        return new ResponseTimeSettings
        {
            TargetUnits = targetUnits,
            TargetValue = targetValue,
            IsTargetRounded = isTargetRounded,
            DeadlineUnits = deadlineUnits,
            DeadlineValue = deadlineValue,
            IsDeadlineRounded = isDeadlineRounded,
            ReadOnly = readOnly,
            UseCustomResponseTimes = useCustomResponseTimes
        };
    }
}

public enum TimeUnits
{
    Days,
    Hours,
    Minutes
}
