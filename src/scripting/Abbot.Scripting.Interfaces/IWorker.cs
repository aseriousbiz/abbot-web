using NodaTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents a person that has working hours and a timezone.
/// </summary>
public interface IWorker
{
    /// <summary>
    /// The working hours during the day for the user.
    /// </summary>
    WorkingHours? WorkingHours { get; }

    /// <summary>
    /// The IANA Time Zone identifier for the member as reported by the chat platform.
    /// </summary>
    DateTimeZone? TimeZone { get; }
}
