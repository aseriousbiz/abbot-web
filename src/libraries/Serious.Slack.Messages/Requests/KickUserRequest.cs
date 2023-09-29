using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The body of the request to remove a user from a room.
/// </summary>
public class KickUserRequest
{
    /// <summary>
    /// Creates a <see cref="KickUserRequest"/>
    /// </summary>
    /// <param name="channel">ID of conversation to remove user from.</param>
    /// <param name="user">Slack User ID to be removed.</param>
    public KickUserRequest(string channel, string user)
    {
        Channel = channel;
        User = user;
    }

    /// <summary>
    /// The ID of the public or private channel to invite user(s) to.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string Channel { get; }

    /// <summary>
    /// Slack User ID to be removed.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public string User { get; }
}
