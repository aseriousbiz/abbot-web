using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

namespace Serious.Abbot.Conversations;

public record ConversationMessageContext(
    Organization Organization,
    Member From,
    Room Room,
    string MessageId,
    string? ThreadId,
    MessageContext? MessageContext = null)
{
    [MemberNotNullWhen(true, nameof(ThreadId))]
    public bool IsInThread => ThreadId is { Length: > 0 } && ThreadId != MessageId;

    /// <summary>
    /// Gets a boolean indicating if the message is "live".
    /// A "live" message is one that we saw via a Slack event as happening _right now_.
    /// If a message isn't live, it's a replay of a previously-sent message.
    /// </summary>
    [MemberNotNullWhen(true, nameof(MessageContext))]
    public bool IsLive => MessageContext is not null;
}

public record ConversationMessage(
    string Text,
    Organization Organization,
    Member From,
    Room Room,
    DateTime UtcTimestamp,
    string MessageId,
    string? ThreadId,
    IReadOnlyList<ILayoutBlock> Blocks,
    IReadOnlyList<FileUpload> Files,
    MessageContext? MessageContext,
    bool Deleted = false) :
    ConversationMessageContext(Organization, From, Room, MessageId, ThreadId, MessageContext)
{
    public IReadOnlyList<Category> Categories => ClassificationResult?.Categories ?? Array.Empty<Category>();

    public ClassificationResult? ClassificationResult { get; init; }

    public IReadOnlyList<SensitiveValue> SensitiveValues { get; init; } = Array.Empty<SensitiveValue>();

    public static ConversationMessage CreateFromLiveMessage(
        MessageContext messageContext,
        IReadOnlyList<SensitiveValue>? sensitiveValues = null,
        ClassificationResult? classifierResult = null)
    {
        return new(
            messageContext.OriginalMessage,
            messageContext.Organization,
            messageContext.FromMember,
            messageContext.Room,
            messageContext.Timestamp.UtcDateTime,
            messageContext.MessageId.Require(),
            messageContext.ThreadId,
            messageContext.Blocks,
            messageContext.Files,
            messageContext)
        {
            ClassificationResult = classifierResult,
            SensitiveValues = sensitiveValues ?? Array.Empty<SensitiveValue>(),
        };
    }
}

public static class ConversationMessageExtensions
{
    /// <summary>
    /// Determines if the sender of the message is a "supportee" member, that is someone who can create conversations.
    /// Usually this is a member of any organization OTHER than the one that owns the room in which the message
    /// was received, or Channel Guests. But for community rooms, it's anyone who is not an Agent.
    /// </summary>
    public static bool IsFromSupportee(this ConversationMessage message)
        => ConversationTracker.IsSupportee(message.From, message.Room);

    /// <inheritdoc cref="SlackFormatter.MessageUrl(string?, string, string, string?)"/>
    /// <param name="message">The <see cref="ConversationMessage"/>.</param>
    public static Uri GetMessageUrl(this ConversationMessage message) =>
        SlackFormatter.MessageUrl(
            message.Organization.Domain,
            message.Room.PlatformRoomId,
            message.MessageId,
            message.ThreadId);
}
