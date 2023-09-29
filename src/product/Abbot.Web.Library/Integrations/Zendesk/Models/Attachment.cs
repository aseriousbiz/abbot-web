using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Serious.Abbot.Integrations.Zendesk.Models;

public class Attachment
{
    /// <summary>
    /// Automatically assigned when created.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public long Id { get; set; }

    /// <summary>
    /// The size of the image file in bytes.
    /// </summary>
    [JsonProperty("size")]
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>
    /// The content type of the image. Example value: "image/png".
    /// </summary>
    [JsonProperty("content_type")]
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    /// <summary>
    /// A full URL where the attachment image file can be downloaded. The file may be hosted externally so take care
    /// not to inadvertently send Zendesk authentication credentials.
    /// </summary>
    [JsonProperty("content_url")]
    [JsonPropertyName("content_url")]
    public string? ContentUrl { get; set; }

    /// <summary>
    /// The name of the image file.
    /// </summary>
    [JsonProperty("file_name")]
    [JsonPropertyName("file_name")]
    public string? FileName { get; set; }

    /// <summary>
    /// If <c>true</c>, the attachment is excluded from the attachment list and the attachment's URL can be referenced
    /// within the comment of a ticket. Default is false
    /// </summary>
    [JsonProperty("inline")]
    [JsonPropertyName("inline")]
    public bool Inline { get; set; }

    /// <summary>
    /// A URL to access the attachment details.
    /// </summary>
    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>
    /// The result of the malware scan. Possible values: "malware_found", "malware_not_found", "failed_to_scan".
    /// </summary>
    [JsonProperty("malware_scan_result")]
    [JsonPropertyName("malware_scan_result")]
    public string? MalwareScanResult { get; set; }

    /// <summary>
    /// The width of the image file in pixels. If unknown, returns null.
    /// </summary>
    [JsonProperty("width")]
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    /// <summary>
    /// The height of the image file in pixels. If unknown, returns null.
    /// </summary>
    [JsonProperty("height")]
    [JsonPropertyName("height")]
    public int? Height { get; set; }
}
