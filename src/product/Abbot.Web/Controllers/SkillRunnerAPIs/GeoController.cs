using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Messages;
using Serious.Abbot.Services;

namespace Serious.Abbot.Controllers;

public class GeoController : SkillRunnerApiControllerBase
{
    readonly IGeocodeService _geoCodeService;

    public GeoController(IGeocodeService geoCodeService)
    {
        _geoCodeService = geoCodeService;
    }

    [HttpGet("geo")]
    public async Task<IActionResult> GetGeocodeAsync(string address, bool includeTimezone)
    {
        var geocode = await _geoCodeService.GetGeocodeAsync(address);

        if (geocode?.Coordinate is null)
        {
            return NotFound();
        }

        var timeZoneId = includeTimezone
            ? await _geoCodeService.GetTimeZoneAsync(geocode.Coordinate.Latitude, geocode.Coordinate.Longitude)
            : null;

        return new ObjectResult(new Location(geocode.Coordinate, geocode.FormattedAddress, timeZoneId));
    }
}
