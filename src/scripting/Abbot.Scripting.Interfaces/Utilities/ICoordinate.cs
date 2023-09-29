namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents a geographic coordinate.
/// </summary>
public interface ICoordinate
{
    /// <summary>
    /// The latitude. Those are the lines that are the belts of the earth.
    /// </summary>
    double Latitude { get; }

    /// <summary>
    /// The longitude. Those are the pin stripes of the earth.
    /// </summary>
    double Longitude { get; }

    /// <summary>
    /// Deconstructs a coordinate into its latitude and longitude.
    /// </summary>
    /// <param name="latitude">The latitude.</param>
    /// <param name="longitude">The longitude.</param>
    void Deconstruct(out double latitude, out double longitude);
}
