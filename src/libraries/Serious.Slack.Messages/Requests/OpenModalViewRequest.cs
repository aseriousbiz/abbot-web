using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Represents a request to open a view in Slack via the
/// <see href="https://api.slack.com/methods/views.open">views.open</see> endpoint.
/// </summary>
public class OpenModalViewRequest : ModalViewRequest
{
    /// <summary>
    /// Constructs a view request with the specified required <c>trigger_id</c>.
    /// </summary>
    /// <param name="triggerId"></param>
    public OpenModalViewRequest(string triggerId)
    {
        TriggerId = triggerId;
    }

    /// <summary>
    /// Exchange a trigger to post to the user.
    /// </summary>
    [JsonProperty("trigger_id")]
    [JsonPropertyName("trigger_id")]
    public string TriggerId { get; }
}
