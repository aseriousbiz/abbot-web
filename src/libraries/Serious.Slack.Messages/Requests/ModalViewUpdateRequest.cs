using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Represents a request to update an existing <see cref="ViewUpdatePayload"/>.
/// </summary>
/// <remarks>
/// <see href="https://api.slack.com/methods/views.update"/> for more information.
/// </remarks>
public class ModalViewUpdateRequest : ModalViewRequest
{
    /// <summary>
    /// A unique identifier of the view to be updated.
    /// Either <see cref="ViewId"/> or <see cref="ExternalId"/> is required.
    /// </summary>
    [JsonProperty("view_id")]
    [JsonPropertyName("view_id")]
    public string? ViewId { get; init; }

    /// <summary>
    /// A unique identifier of the view set by the developer. Must be unique for all views on a team.
    /// Max length of 255 characters. Either <c>view_id</c> or <c>external_id</c> is required.
    /// </summary>
    [JsonProperty("external_id")]
    [JsonPropertyName("external_id")]
    public string? ExternalId { get; init; }

    /// <summary>
    /// A string that represents view state to protect against possible race conditions.
    /// Ex. <c>156772938.1827394</c>
    /// </summary>
    [JsonProperty("hash")]
    [JsonPropertyName("hash")]
    public string? Hash { get; init; }
}
