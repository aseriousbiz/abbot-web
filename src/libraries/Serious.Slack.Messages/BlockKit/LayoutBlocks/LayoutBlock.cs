using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Slack.Abstractions;

/// <summary>
/// Base class for Slack Layout blocks. These are the building blocks for a block kit message.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks"/> for more information. Examples include:
/// <list type="bullet">
/// <item><see cref="Actions"/></item>
/// <item><see cref="Context"/></item>
/// <item><see cref="Divider"/></item>
/// <item><see cref="FileBlock"/></item>
/// <item><see cref="Header"/></item>
/// <item><see cref="Image"/></item>
/// <item><see cref="Input"/></item>
/// <item><see cref="Section"/></item>
/// </list>
/// </remarks>
public abstract record LayoutBlock(string Type) : Element(Type), ILayoutBlock
{
    /// <summary>
    /// A string acting as a unique identifier for a block. If not specified, it will be generated. You can use this
    /// when you receive an interaction payload to identify the source of the action. Maximum length for this field
    /// is 255 characters. This should be unique for each message and each iteration of a message.
    /// If a message is updated, use a new value.
    /// </summary>
    [JsonProperty("block_id")]
    [JsonPropertyName("block_id")]
    public string? BlockId { get; init; }
}
