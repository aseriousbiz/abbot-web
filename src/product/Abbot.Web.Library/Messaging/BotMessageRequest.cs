using System.Collections.Generic;
using Serious.Abbot.Messages;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.Messaging;

/// <summary>
/// This is our abstraction of a message sent to chat via the Slack message dispatcher.
/// </summary>
/// <param name="Text">The message to send to chat. If <see cref="Blocks"/> are present, this is the fallback text.</param>
/// <param name="Blocks">The Block Kit blocks to send as part of a message to Slack</param>
/// <param name="Attachments">The set of legacy attachments to send as part of the message, if any.</param>
/// <param name="To">Where to send the message.</param>
/// <param name="ImageUpload">The image to upload, if any.</param>
/// <param name="MessageMetadata">Metadata to attach to the message, if any.</param>
public record BotMessageRequest(
    string Text,
    ChatAddress To,
    IEnumerable<ILayoutBlock>? Blocks = null,
    IEnumerable<LegacyMessageAttachment>? Attachments = null,
    ImageUpload? ImageUpload = null,
    MessageMetadata? MessageMetadata = null);
