using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The body of a request to change the purpose of a channel.
/// </summary>
public class PurposeRequest
{
    /// <summary>
    /// Constructs a <see cref="PurposeRequest"/>.
    /// </summary>
    /// <param name="channel">The Id of the channel.</param>
    /// <param name="purpose">The new purpose.</param>
    public PurposeRequest(string channel, string purpose)
    {
        Channel = channel;
        Purpose = purpose;
    }

    /// <summary>
    /// The Id of the channel.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string Channel { get; }

    /// <summary>
    /// The new purpose.
    /// </summary>
    [JsonProperty("purpose")]
    [JsonPropertyName("purpose")]
    public string Purpose { get; }
}
