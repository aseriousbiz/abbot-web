using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Base type for all Slack responses.
/// </summary>
public class ApiResponse
{
    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(false, nameof(Error))]
    public virtual bool Ok { get; init; }

    /// <summary>
    /// If <see cref="Ok"/> is false, then this contains an error message for why the API call failed.
    /// </summary>
    [JsonProperty("error")]
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    /// More metadata about requests, errors, and warnings.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/changelog/2016-09-28-response-metadata-is-on-the-way"/> for more
    /// information.
    /// </remarks>
    [JsonProperty("response_metadata")]
    [JsonPropertyName("response_metadata")]
    public ResponseMetadata? ResponseMetadata { get; init; }

    /// <summary>
    /// Returns the string "Ok!" when <see cref="Ok"/> is <c>true</c>, otherwise returns
    /// the <see cref="Error"/> and <see cref="ResponseMetadata"/> as a string.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        return Ok
            ? "Ok!"
            : $"Error: {Error}\n{ResponseMetadata}";
    }
}
