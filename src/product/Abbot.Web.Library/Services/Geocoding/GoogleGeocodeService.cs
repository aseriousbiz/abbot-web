using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Messages;

namespace Serious.Abbot.Services;

public class GoogleGeocodeService : IGeocodeService
{
    readonly HttpClient _httpClient;
    readonly GoogleOptions _options;

    public GoogleGeocodeService(IOptions<GoogleOptions> options, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    string ApiKey => _options.GeolocationApiKey
                     ?? throw new InvalidOperationException("Google:GeolocationApiKey not configured in app settings.");


    public async Task<Geocode?> GetGeocodeAsync(string address)
    {
        var geoBaseUrl = $"https://maps.googleapis.com/maps/api/geocode/json?key={ApiKey}&address=";

        var geoEndpoint = new Uri(geoBaseUrl + HttpUtility.UrlEncode(address));
        var response = await _httpClient.GetJsonAsync<GoogleGeoCodingResponse>(geoEndpoint);

        var result = response.Results.FirstOrDefault();
        if (result is null)
        {
            return null;
        }

        var location = result.Geometry.Location;

        return new Geocode(new Coordinate(location.Lat, location.Lng), result.FormattedAddress);
    }

    public async Task<string?> GetTimeZoneAsync(double latitude, double longitude)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var tzBaseUrl = $"https://maps.googleapis.com/maps/api/timezone/json?key={ApiKey}&timestamp={timestamp}";

        var tzEndpoint = new Uri(tzBaseUrl + $"&location={latitude},{longitude}");
        var tzResult = await _httpClient.GetJsonAsync<GoogleTimeZoneResponse>(tzEndpoint);
        return tzResult.TimeZoneId;
    }
}
