using System.Text.Json.Serialization;

namespace Serious.Abbot.Playbooks.Outputs;

public record MessageOutput
{
    [JsonPropertyName("channel")]
    public required ChannelOutput Channel { get; init; }

    [JsonPropertyName("ts")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("thread_ts")]
    public string? ThreadTimestamp { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("url")]
    public Uri? Url { get; init; }
}
