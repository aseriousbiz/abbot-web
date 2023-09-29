using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Services;

public class GoogleGeocodeResult
{
    [JsonProperty("formatted_address")]
    [JsonPropertyName("formatted_address")]
    public string FormattedAddress { get; set; } = null!;

    public GoogleGeometry Geometry { get; set; } = null!;
}
