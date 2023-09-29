using System.Collections.Generic;
using Microsoft.Bot.Schema;

namespace Serious.Abbot.Messages;

/// <summary>
/// Message sent from the skill runners to Abbot to reply in chat.
/// </summary>
public class ProactiveBotMessage
{
    /// <summary>
    /// The skill that is sending the message.
    /// </summary>
    public int SkillId { get; init; }

    /// <summary>
    /// The message to send to chat.
    /// </summary>
    public string Message { get; set; } = null!;

    /// <summary>
    /// The conversation reference used to send a proactive message to chat.
    /// </summary>
    public ConversationReference ConversationReference { get; init; } = null!;

    // When to send this response.
    public long Schedule { get; init; }

    /// <summary>
    /// The set of attachments to send as part of the message, if any.
    /// </summary>
    public IReadOnlyList<MessageAttachment>? Attachments { get; set; }

    /// <summary>
    /// Options to customize how a message is sent.
    /// </summary>
    public ProactiveBotMessageOptions? Options { get; init; }

    /// <summary>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// </summary>
    public string? ContextId { get; init; }

    /// <summary>
    /// A JSON string representation of the Block Kit blocks to send as part of a message to Slack.
    /// </summary>
    /// <remarks>
    /// <para>
    /// We support several options and interpret it on the app.ab.bot side.
    /// </para>
    /// <para>
    /// For example, if the Blocks is an array, we assume it's an array of layout blocks.
    /// </para>
    /// <para>
    /// If it's a single object, we look to see if it has a "blocks" property. If so, we take the blocks property
    /// as an array of layout blocks (this supports the common case of someone using the Slack Block Kit Builder).
    /// </para>
    /// <para>
    /// If it's a single object without a "blocks" property, we assume it's a single layout block.
    /// </para>
    /// </remarks>
    public string? Blocks { get; init; }
}
