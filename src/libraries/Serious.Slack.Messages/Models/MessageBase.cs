using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Base class for different message payloads such as the response from posting a new message or
/// the original message as part of an interactive message event.
/// </summary>
/// <param name="Text">
/// The formatted text of the message to be published. If blocks are included, this will become the fallback
/// text used in notifications.
/// </param>
public abstract record MessageBase(
    [property: JsonProperty("text")]
    [property: JsonPropertyName("text")]string? Text)
{
    /// <summary>
    /// Set the bot's user name. Must be used in conjunction with <c>as_user</c> set to false,
    /// otherwise ignored.
    /// </summary>
    [JsonProperty("username")]
    [JsonPropertyName("username")]
    public string? UserName { get; init; }
};
