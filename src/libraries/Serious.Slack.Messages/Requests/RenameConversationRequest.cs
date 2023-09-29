using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The body of the request to rename a channel.
/// </summary>
public class RenameConversationRequest
{
    /// <summary>
    /// Creates an instance of <see cref="UsersInviteRequest"/>
    /// </summary>
    /// <param name="channel">The ID of the public or private channel to invite user(s) to.</param>
    /// <param name="name">New name for conversation.</param>
    public RenameConversationRequest(string channel, string name)
    {
        Channel = channel;
        Name = name;
    }

    /// <summary>
    /// The ID of the public or private channel to rename.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string Channel { get; }

    /// <summary>
    /// New name for conversation.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; }
}
