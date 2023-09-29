using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Payloads;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Interface for <see href="https://api.slack.com/reference/block-kit/block-elements">Block Elements</see>. Block
/// elements can be used inside of <c>section</c> (<see cref="Section"/>), <c>context</c>
/// (<see cref="Context"/>), <c>input</c> (<see cref="Input"/>), and <c>actions</c>
/// (<see cref="Actions"/>) layout blocks.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements">block elements</see> for more information.
/// </remarks>
public interface IBlockElement : IElement
{
}

/// <summary>
/// Interface for an element that can be added to the <c>elements</c> of an <c>actions</c> block (<see cref="Actions"/>).
/// </summary>
public interface IActionElement : IPayloadElement
{
}

/// <summary>
/// <para>
/// An <see href="https://api.slack.com/reference/block-kit/interactive-components">interactive component</see>
/// (<see cref="IBlockElement"/>) that's part of a <c>block_actions</c> payload (<see cref="BlockActionsPayload"/>).
/// </para>
/// <para>
/// When a user interacts with an interactive component in a Slack message, a <c>block_actions</c> payload is sent to
/// the bot containing an <c>actions</c> property containing the interactive components that the user interacted with,
/// with a little extra information appended to the component. This interface represents that extra information.
/// </para>
/// </summary>
/// <remarks>
/// Types should only implement this explicitly. These values should never be set by the user, but only by incoming
/// JSON payloads.
/// </remarks>
public interface IPayloadElement : IBlockElement
{
    /// <summary>
    /// An identifier for this action. You can use this when you receive an interaction payload to identify
    /// the source of the action. Should be unique among all other action_ids in the containing block.
    /// Maximum length for this field is 255 characters.
    /// </summary>
    [JsonProperty("action_id")]
    [JsonPropertyName("action_id")]
    string ActionId { get; }

    /// <summary>
    /// When receiving a <c>block_actions</c> payload, the <c>actions</c> contains specific interactive components
    /// that were used. In that case, this contains the parent block id of the element involved in the interaction,
    /// otherwise it's null.
    /// </summary>
    [JsonProperty("block_id")]
    [JsonPropertyName("block_id")]
    string? BlockId { get; set; }

    /// <summary>
    /// The timestamp for when the user interacted with this element.
    /// </summary>
    [JsonProperty("action_ts")]
    [JsonPropertyName("action_ts")]
    string? ActionTimestamp { get; init; }
}

/// <summary>
/// An interactive component (<see cref="IPayloadElement"/>) that has a single string value. This provides a generic
/// way to retrieve the value from any component that has a single value without casting to the specific type and
/// using the specific property.
/// </summary>
public interface IValueElement : IPayloadElement
{
    /// <summary>
    /// The user selected or supplied value of the interactive component.
    /// </summary>
    string? Value { get; init; }
}

/// <summary>
/// An interactive component (<see cref="IPayloadElement"/>) that has multiple string values. This provides a generic
/// way to retrieve the values from any component that has multiple values without casting to the specific type and
/// using the specific property.
/// </summary>
public interface IMultiValueElement : IPayloadElement
{
    /// <summary>
    /// The user selected values of the interactive component.
    /// </summary>
    public IReadOnlyList<string> Values { get; init; }
}
