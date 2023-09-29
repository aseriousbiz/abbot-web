using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about a Conversation topic
/// </summary>
public class TopicInfo
{
    /// <summary>
    /// The topic of the conversation.
    /// </summary>
    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public string Value { get; set; } = null!;

    /// <summary>
    /// The Slack Id of the creator of the topic.
    /// </summary>
    [JsonProperty("creator")]
    [JsonPropertyName("creator")]
    public string Creator { get; set; } = null!;

    /// <summary>
    /// A timestamp of when the topic was last set.
    /// </summary>
    [JsonProperty("last_set")]
    [JsonPropertyName("last_set")]
    public long LastSet { get; set; }

    /// <summary>
    /// The date the topic was last set calculated from the <see cref="LastSet"/> timestamp.
    /// </summary>
    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeOffset LastSetDate => DateTimeOffset.FromUnixTimeSeconds(LastSet);
}
