using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Converters;

namespace Serious.Slack.Events;

/// <summary>
/// Base class for the reaction events such as <c>reaction_added</c> and <c>reaction_removed</c>.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/events/reaction_added"/>
/// </remarks>
public abstract record ReactionEvent(string Type) : EventBody(Type)
{
    /// <summary>
    /// The emoji name used for the reaction (sans colons).
    /// </summary>
    [JsonProperty("reaction")]
    [JsonPropertyName("reaction")]
    public string Reaction { get; init; } = null!;

    /// <summary>
    /// The item the reaction is a reaction to.
    /// </summary>
    [JsonProperty("item")]
    [JsonPropertyName("item")]
    public ReactionItem Item { get; init; } = null!;
}

/// <summary>
/// Information about the <c>reaction_added</c> event.
/// </summary>
[Element("reaction_added")]
public sealed record ReactionAddedEvent() : ReactionEvent("reaction_added");

/// <summary>
/// Information about the <c>reaction_removed</c> event.
/// </summary>
[Element("reaction_removed")]
public sealed record ReactionRemovedEvent() : ReactionEvent("reaction_removed");

/// <summary>
/// Contains information about the item the reaction was to.
/// </summary>
/// <param name="Type">The type of reaction. <c>message</c>, <c>file</c>, or <c>file_comment</c></param>
/// <param name="Channel">For <c>message</c> reactions, this is the channel the reaction occurred in.</param>
/// <param name="Timestamp">For <c>message</c> reactions, this is the message timestamp.</param>
/// <param name="File">For <c>file</c> and <c>file_comment</c> reactions, this is the Id of the file.</param>
/// <param name="FileComment">For <c>file_comment</c>, this is the Id of the comment.</param>
public record ReactionItem(

    [property: JsonProperty("type")]
    [property: JsonPropertyName("type")]
    string Type,

    [property: JsonProperty("channel")]
    [property: JsonPropertyName("channel")]
    string? Channel,

    [property: JsonProperty("ts")]
    [property: JsonPropertyName("ts")]
    string? Timestamp,

    [property: JsonProperty("file")]
    [property: JsonPropertyName("file")]
    string? File = null,

    [property: JsonProperty("file_comment")]
    [property: JsonPropertyName("file_comment")]
    string? FileComment = null);
