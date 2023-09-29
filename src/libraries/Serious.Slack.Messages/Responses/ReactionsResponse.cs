using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Response from the <c>reactions.get</c> API.
/// </summary>
public class ReactionsResponse : InfoResponse<ItemWithReactions>
{
    /// <summary>
    /// The type of item to grab the reactions for.
    /// </summary>
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; init; } = null!;

    /// <summary>
    /// Whether the API call was successful or not.
    /// </summary>
    [JsonProperty("ok")]
    [JsonPropertyName("ok")]
    [MemberNotNullWhen(true, nameof(Body))]
    public override bool Ok { get; init; }

    /// <summary>
    /// The message with the reactions.
    /// </summary>
    [JsonProperty("message")]
    [JsonPropertyName("message")]
    public override ItemWithReactions? Body { get; init; }
}

/// <summary>
/// Represents a message with reactions returned from the <c>reactions.get</c> API.
/// </summary>
/// <param name="Type">The type of item to grab the reactions for.</param>
/// <param name="Text">The text of the item or message.</param>
/// <param name="User">The user that created the item.</param>
/// <param name="Timestamp">The timestamp when the item was created.</param>
/// <param name="Team">The team the item belongs to.</param>
/// <param name="Permalink">A link to the item.</param>
public record ItemWithReactions(

    [property: JsonProperty("type")]
    [property: JsonPropertyName("type")]
    string Type,

    [property: JsonProperty("text")]
    [property: JsonPropertyName("text")]
    string Text,

    [property: JsonProperty("user")]
    [property: JsonPropertyName("user")]
    string User,

    [property: JsonProperty("ts")]
    [property: JsonPropertyName("ts")]
    string Timestamp,

    [property: JsonProperty("team")]
    [property: JsonPropertyName("team")]
    string Team,

    [property: JsonProperty("permalink")]
    [property: JsonPropertyName("permalink")]
    Uri Permalink)
{
    /// <summary>
    /// The reactions on the item.
    /// </summary>
    /// <remarks>
    /// The actual JSON payload omits this if there's no reactions, rather than return an empty array.
    /// That's why this is a "normal" property, so we can default that case to an empty array.
    /// </remarks>
    [JsonProperty("reactions")]
    [JsonPropertyName("reactions")]
    public IReadOnlyList<ReactionSummary> Reactions { get; init; } = Array.Empty<ReactionSummary>();
}

/// <summary>
/// Represents a reaction summary returned from the <c>reactions.get</c> API.
/// </summary>
/// <param name="Name">The name of the reaction.</param>
/// <param name="Count">The number of reactions of this type.</param>
/// <param name="Users">The set of users that reacted with this reaction.</param>
public record ReactionSummary(

    [property: JsonProperty("name")]
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonProperty("count")]
    [property: JsonPropertyName("count")]
    int Count,

    [property: JsonProperty("users")]
    [property: JsonPropertyName("users")]
    IReadOnlyList<string> Users);
