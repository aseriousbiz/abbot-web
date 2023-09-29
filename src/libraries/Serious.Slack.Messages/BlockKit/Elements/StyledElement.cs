using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;

namespace Serious.Slack.Payloads;

/// <summary>
/// Base class for an element that has a <c>style</c> (<see cref="TextStyle"/>) applied to it.
/// </summary>
public abstract record StyledElement(string Type) : Element(Type)
{
    /// <summary>
    /// The style applied to the element.
    /// </summary>
    [JsonProperty("style")]
    [JsonPropertyName("style")]
    public TextStyle? Style { get; init; }
}
