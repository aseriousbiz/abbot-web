using System.Collections.Generic;

namespace Serious.Abbot.Services;

public class GoogleGeoCodingResponse
{
    public List<GoogleGeocodeResult> Results { get; set; } = new();
}
