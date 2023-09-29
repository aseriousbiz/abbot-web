using Serious.Slack.Abstractions;
using Serious.Slack.Converters;
using Serious.Slack.Payloads;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Interface for <see href="https://api.slack.com/reference/block-kit/blocks">Layout blocks</see>. These are the
/// building blocks for a block kit message.
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
[Newtonsoft.Json.JsonConverter(typeof(ElementConverter))]
public interface ILayoutBlock : IElement
{
    /// <summary>
    /// A string acting as a unique identifier for a block. If not specified, it will be generated. You can use this
    /// when you receive an interaction payload to identify the source of the action. Maximum length for this field
    /// is 255 characters. This should be unique for each message and each iteration of a message.
    /// If a message is updated, use a new value.
    /// </summary>
    string? BlockId { get; }
}
