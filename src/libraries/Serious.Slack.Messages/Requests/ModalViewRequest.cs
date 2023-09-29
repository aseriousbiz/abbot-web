using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Base class for the modal view requests.
/// </summary>
public abstract class ModalViewRequest
{
    /// <summary>
    /// A view payload.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    public ViewUpdatePayload ViewUpdate { get; init; } = null!;
}
