using System;

namespace Serious;

public static class TimespanExtensions
{
    public static string FormatDuration(this TimeSpan duration)
    {
        var seconds = duration.TotalSeconds;

        return seconds switch
        {
            < 60 => ((int)seconds).ToQuantity("second"),
            < 3600 => duration.FormatMinutes(),
            _ => duration.FormatHours()
        };
    }

    static string FormatMinutes(this TimeSpan duration)
    {
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;

        return minutes.ToQuantity("minute")
               + (seconds > 0 ? $" and {seconds.ToQuantity("second")}" : "");
    }

    static string FormatHours(this TimeSpan duration)
    {
        var hours = duration.Hours;
        var minutes = duration.Minutes;
        var seconds = duration.Seconds;

        var append = (minutes, seconds) switch
        {
            (0, 0) => "",
            (_, 0) => $" and {minutes.ToQuantity("minute")}",
            (0, _) => $" and {seconds.ToQuantity("second")}",
            _ => $", {minutes.ToQuantity("minute")}, and {seconds.ToQuantity("second")}"
        };

        return hours.ToQuantity("hour") + append;
    }
}
