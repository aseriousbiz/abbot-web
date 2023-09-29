using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;

namespace Serious.Slack.Abstractions;

/// <summary>
/// Base class for interactive <see href="https://api.slack.com/reference/block-kit/block-elements">block elements</see>.
/// These are all the block elements except <c>image</c> (<see cref="ImageElement"/>).
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements">block elements</see> for more information.
/// </remarks>
public abstract record InteractiveElement(string Type) : Element(Type), IPayloadElement
{
    /// <summary>
    /// An identifier for this action. You can use this when you receive an interaction payload to identify
    /// the source of the action. Should be unique among all other action_ids in the containing block.
    /// Maximum length for this field is 255 characters.
    /// </summary>
    [JsonProperty("action_id")]
    [JsonPropertyName("action_id")]
    public string ActionId { get; init; } = null!;

    /// <summary>
    /// When receiving a <c>block_actions</c> payload, the <c>actions</c> contains specific interactive components
    /// that were used. In that case, this contains the parent block id of the element involved in the interaction,
    /// otherwise it's null.
    /// </summary>
    [JsonProperty("block_id")]
    [JsonPropertyName("block_id")]
    public string? BlockId { get; set; }

    /// <summary>
    /// The timestamp for when the user interacted with this element.
    /// </summary>
    [JsonProperty("action_ts")]
    [JsonPropertyName("action_ts")]
    string? IPayloadElement.ActionTimestamp { get; init; }
}
