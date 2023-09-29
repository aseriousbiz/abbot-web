using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// The body of a request to change the topic of a channel.
/// </summary>
/// <param name="Channel">The Id of the channel.</param>
/// <param name="Topic">The new topic.</param>
public record TopicRequest(
    [property:JsonProperty("channel")]
    [property:JsonPropertyName("channel")]
    string Channel,

    [property:JsonProperty("topic")]
    [property:JsonPropertyName("topic")]
    string Topic);

