using System.Text.Json.Serialization;

namespace Serious.Abbot.Playbooks;

public record SelectedOption
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }
    [JsonPropertyName("label")]
    public string? Label { get; init; }
}
