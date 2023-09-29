using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Services;

namespace Serious.TestHelpers
{
    public class FakeGeocodingService : IGeocodeService
    {
        readonly Dictionary<string, Geocode> _geocodes = new();

        public void AddGeocode(string address, Geocode geocode)
        {
            _geocodes.Add(address, geocode);
        }

        public Task<Geocode?> GetGeocodeAsync(string address)
        {
            return _geocodes.TryGetValue(address, out var geocode)
                ? Task.FromResult((Geocode?)geocode)
                : Task.FromResult((Geocode?)null);
        }

        public Task<string?> GetTimeZoneAsync(double latitude, double longitude)
        {
            return Task.FromResult((string?)null);
        }
    }
}
