using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The body of a request to invite a user or set of users to a channel.
/// </summary>
public class UsersInviteRequest
{
    /// <summary>
    /// Creates an instance of <see cref="UsersInviteRequest"/>
    /// </summary>
    /// <param name="channel">The ID of the public or private channel to invite user(s) to.</param>
    /// <param name="users">List of Slack User Ids of the users to invite.</param>
    public UsersInviteRequest(string channel, IEnumerable<string> users)
    {
        Channel = channel;
        Users = string.Join(",", users);
    }

    /// <summary>
    /// The ID of the public or private channel to invite user(s) to.
    /// </summary>
    [JsonProperty("channel")]
    [JsonPropertyName("channel")]
    public string Channel { get; }

    /// <summary>
    /// A comma separated list of user IDs. Up to 1000 users may be listed.
    /// </summary>
    [JsonProperty("users")]
    [JsonPropertyName("users")]
    public string Users { get; }
}

/// <summary>
/// A request to join a conversation.
/// </summary>
/// <param name="Channel">The ID of the public or private channel to invite user(s) to.</param>
public record ConversationJoinRequest(
    [property: JsonProperty("channel")]
    [property: JsonPropertyName("channel")]
    string Channel);
