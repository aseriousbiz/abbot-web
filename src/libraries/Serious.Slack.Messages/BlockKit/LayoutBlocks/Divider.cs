using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.BlockKit;

/// <summary>
/// A content divider, like an <c>&lt;hr&gt;</c>, to split up different blocks inside of a message.
/// The divider block is nice and neat, requiring only a <c>type</c>.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/blocks#divider"/> for more info.
/// <para>
/// Available in surfaces: Modals Messages Home tabs
/// </para>
/// </remarks>
[Element("divider")]
public record Divider() : LayoutBlock("divider");
