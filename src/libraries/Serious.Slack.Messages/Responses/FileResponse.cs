using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Response from calling the <c>files.upload</c> Slack API.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/methods/files.upload" /> for more info.
/// </remarks>
public class FileResponse : InfoResponse<UploadedFile>
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The uploaded file.
    /// </summary>
    [JsonProperty("file")]
    [JsonPropertyName("file")]
    public override UploadedFile? Body { get; init; }
}
