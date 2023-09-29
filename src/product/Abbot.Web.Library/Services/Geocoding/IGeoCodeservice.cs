using System.Threading.Tasks;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Services;

/// <summary>
/// Provides geocoding functionality.
/// </summary>
public interface IGeocodeService
{
    /// <summary>
    /// Returns a geocoded location.
    /// </summary>
    /// <param name="address">The address to geocode.</param>
    Task<Geocode?> GetGeocodeAsync(string address);

    /// <summary>
    /// Given a latitude and longitude, returns the timezone id.
    /// </summary>
    /// <param name="latitude">The latitude.</param>
    /// <param name="longitude">The longitude.</param>
    Task<string?> GetTimeZoneAsync(double latitude, double longitude);
}
