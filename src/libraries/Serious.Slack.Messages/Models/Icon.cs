using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Avatar Icon.
/// </summary>
public class Icon
{
    /// <summary>
    /// The 34x34 pixel icon.
    /// </summary>
    [JsonProperty("image_34")]
    [JsonPropertyName("image_34")]
    public string? Image34 { get; set; }

    /// <summary>
    /// The 44x44 pixel icon.
    /// </summary>
    [JsonProperty("image_44")]
    [JsonPropertyName("image_44")]
    public string? Image44 { get; set; }

    /// <summary>
    /// The 68x68 pixel icon.
    /// </summary>
    [JsonProperty("image_68")]
    [JsonPropertyName("image_68")]
    public string? Image68 { get; set; }

    /// <summary>
    /// The 88x88 pixel icon.
    /// </summary>
    [JsonProperty("image_88")]
    [JsonPropertyName("image_88")]
    public string? Image88 { get; set; }

    /// <summary>
    /// The 102x102 pixel icon.
    /// </summary>
    [JsonProperty("image_102")]
    [JsonPropertyName("image_102")]
    public string? Image102 { get; set; }

    /// <summary>
    /// The 132x132 pixel icon.
    /// </summary>
    [JsonProperty("image_132")]
    [JsonPropertyName("image_132")]
    public string? Image132 { get; set; }

    /// <summary>
    /// The 230x230 pixel icon.
    /// </summary>
    [JsonProperty("image_230")]
    [JsonPropertyName("image_230")]
    public string? Image230 { get; set; }

    /// <summary>
    /// The original image.
    /// </summary>
    [JsonProperty("image_original")]
    [JsonPropertyName("image_original")]
    public string? ImageOriginal { get; set; }
}
