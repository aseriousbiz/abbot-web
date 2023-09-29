using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents a location returned by a geocoding service.
/// </summary>
public class Geocode
{
    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Geocode()
    {
    }

    public Geocode(ICoordinate? coordinate, string? formattedAddress)
    {
        Coordinate = coordinate;
        FormattedAddress = formattedAddress;
    }

    /// <summary>
    /// The coordinates of the location.
    /// </summary>
    public ICoordinate? Coordinate { get; init; }

    /// <summary>
    /// The formatted address for the location.
    /// </summary>
    public string? FormattedAddress { get; init; }
}
