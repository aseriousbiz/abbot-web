using Newtonsoft.Json;
using Serious.Slack.Abstractions;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// An option in a select menu.
/// </summary>
public class SelectOption
{
    /// <summary>
    /// The text of the select option.
    /// </summary>
    [JsonProperty(PropertyName = "text")]
    public TextObject? Text { get; init; }

    /// <summary>
    /// The value of the select option.
    /// </summary>
    [JsonProperty(PropertyName = "value")]
    public string? Value { get; init; }
}
