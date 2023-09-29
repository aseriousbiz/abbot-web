using System;
using System.Collections.Generic;
using Microsoft.Bot.Schema;
using Serious.Slack.BlockKit;

namespace Serious.Slack.BotFramework.Model;

/// <summary>
/// An <see cref="Activity"/> that includes rich formatted message and fallback text. Only Slack is currently supported.
/// </summary>
public class RichActivity : Activity
{
    /// <summary>
    /// Constructs an instance of <see cref="RichActivity"/>.
    /// </summary>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public RichActivity(string fallbackText, params ILayoutBlock[] blocks) : base(ActivityTypes.Message)
    {
        Text = fallbackText;
        Attachments = new List<Attachment>();
        Entities = new List<Entity>();
        Blocks = blocks.ToReadOnlyList();
    }

    /// <summary>
    /// The Slack user id of the user who will receive the ephemeral message. The user should be in the channel
    /// specified by the channel argument. Default is <c>null</c>.
    /// </summary>
    public string? EphemeralUser { get; init; }

    /// <summary>
    /// Slack BlockKit blocks. This can be one or more layout blocks.
    /// </summary>
    public IReadOnlyList<ILayoutBlock> Blocks { get; }

    /// <summary>
    /// For some payloads, such as ephemeral messages, this URL provides an endpoint to edit or delete the message.
    /// </summary>
    public Uri? ResponseUrl { get; set; }
}
