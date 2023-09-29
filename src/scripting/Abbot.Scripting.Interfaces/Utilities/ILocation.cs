using NodaTime;

namespace Serious.Abbot.Scripting;

/// <summary>
/// A geo-coded location.
/// </summary>
public interface ILocation
{
    /// <summary>
    /// The coordinates of the location.
    /// </summary>
    ICoordinate? Coordinate { get; }

    /// <summary>
    /// The formatted address for the location.
    /// </summary>
    string? FormattedAddress { get; }

    /// <summary>
    /// The timezone for the location, if it was retrieved.
    /// </summary>
    DateTimeZone? TimeZone { get; }
}
