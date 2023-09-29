using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The body of the request to create a new channel.
/// </summary>
public class ConversationCreateRequest
{
    /// <summary>
    /// Constructs a new <see cref="ConversationCreateRequest"/>.
    /// </summary>
    /// <param name="name">The name of the channel.</param>
    /// <param name="isPrivate">If <c>true</c>, the channel is private.</param>
    public ConversationCreateRequest(string name, bool isPrivate = false)
    {
        Name = name;
        IsPrivate = isPrivate;
    }

    /// <summary>
    /// The name of the channel to create.
    /// </summary>
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string Name { get; }

    /// <summary>
    /// Whether to create a private channel or not.
    /// </summary>
    [JsonProperty("is_private")]
    [JsonPropertyName("is_private")]
    public bool IsPrivate { get; }
}
