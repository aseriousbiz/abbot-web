using System.Collections.Generic;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Eventing.Messages;

/// <summary>
/// An interior type (not intended as a bus message) used to represent the content of a Slack message.
/// </summary>
public record MessageContent
{
    public required string Text { get; init; }
    public IReadOnlyList<ILayoutBlock>? Blocks { get; init; }
}
