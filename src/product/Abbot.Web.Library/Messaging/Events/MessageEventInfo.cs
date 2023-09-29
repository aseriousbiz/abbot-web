using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Serious.Abbot.Messaging;
using Serious.Payloads;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Serious.Text;

namespace Serious.Abbot.Events;

/// <summary>
/// A normalized event that is sent when a user enters a chat message or interacts
/// with a UI element in a message.
/// </summary>
/// <remarks>
/// This instance is created when receiving an event, but before we resolve a
/// <see cref="IPlatformMessage"/>.
/// </remarks>
/// <param name="Text">The text of the message.</param>
/// <param name="PlatformRoomId">The platform-specific id for the room.</param>
/// <param name="PlatformUserId">The platform-specific id for the user that sent the message or did the interaction.</param>
/// <param name="MentionedUserIds">The platform-specific user IDs of the users mentioned in the message.</param>
/// <param name="DirectMessage">If <c>true</c>, this is a direct message.</param>
/// <param name="Ignore">If <c>true</c>, the message should be ignored.</param>
/// <param name="MessageId">Slack timestamp for the current message or event.</param>
/// <param name="ThreadId">Slack timestamp for the root message in the thread. If <c>null</c>, the message is not in a thread.</param>
/// <param name="InteractionInfo">The interaction info for the message or event.</param>
/// <param name="WorkflowMessage">If this message is posted as the result of a workflow on behalf of a user, this is <c>true</c>.</param>
public record MessageEventInfo(
    string Text,
    string PlatformRoomId,
    string PlatformUserId,
    IReadOnlyList<string> MentionedUserIds,
    bool DirectMessage,
    bool Ignore,
    string? MessageId,
    string? ThreadId,
    MessageInteractionInfo? InteractionInfo,
    IReadOnlyList<ILayoutBlock> Blocks,
    IReadOnlyList<FileUpload> Files,
    bool WorkflowMessage = false)
{
    /// <summary>
    /// Create a <see cref="MessageEventInfo"/> from an incoming Slack message.
    /// </summary>
    /// <param name="text">The text of the message.</param>
    /// <param name="messageEvent">The Slack message event.</param>
    /// <param name="botUserId">The bot user id.</param>
    public static MessageEventInfo? FromSlackMessageEvent(string text, MessageEvent messageEvent, string botUserId)
    {
        if (messageEvent.Channel is not { Length: > 0 } platformRoomId
            || GetUserIdFromMessageEvent(messageEvent) is not { Length: > 0 } platformUserId)
        {
            // If we can't determine the room or user, we need to ignore this message.
            // This should never happen in practice.
            return null;
        }

        var mentionedUserIds = messageEvent
            .Blocks
            .Cast<IElement>()
            .Flatten(e => e is IBlockWithElements blockWithElements
                ? blockWithElements.Elements
                : Enumerable.Empty<IElement>())
            .OfType<UserMention>()
            .Select(m => m.UserId)
            .ToList();

        var botMentioned = messageEvent.Type is "app_mention"
            || mentionedUserIds.Any(id => id.Equals(botUserId, StringComparison.Ordinal));

        if (botMentioned)
        {
            // Get the Ids without Abbot.
            mentionedUserIds = mentionedUserIds
                .Where(id => !id.Equals(botUserId, StringComparison.Ordinal))
                .ToList();
        }
        // If we have a MessageEvent, it includes a channel_type we should use instead of Conversation.IsGroup
        // Conversation.IsGroup is only true if there are actually more than 2 users/bots in a conversation.
        // Even a Slack channel with only a user and Abbot in it comes back as Conversation.IsGroup == false.
        // Fortunately, we only actually _care_ about DirectMessage for incoming message events.
        var directMessage = messageEvent.ChannelType is "im";

        // A message posted as a result of a workflow event.
        bool workflowMessage = messageEvent is BotMessageEvent { BotProfile.IsWorkflowBot: true };

        // When a message mentions Abbot, Slack sends us two events - "app_mention" and "message". To avoid
        // double responding, we need to respond to only one of these. So we ignore "message" when Abbot is mentioned
        // because the "app_mention" event would also be raised in that situation.
        // </remarks>
        var ignore = !directMessage && messageEvent is not AppMentionEvent && botMentioned;

        return Create(
            text,
            platformRoomId,
            platformUserId,
            messageEvent.Timestamp,
            messageEvent.ThreadTimestamp,
            mentionedUserIds,
            null,
            directMessage,
            ignore,
            messageEvent.Blocks,
            messageEvent is FileShareMessageEvent fileShareMessageEvent
                ? fileShareMessageEvent.Files
                : Array.Empty<FileUpload>(),
            workflowMessage);
    }

    // Ok, here's where we do something really ugly, but Slack forced our hand on this.
    // When Slack posts a message into a channel from a Form Workflow Step, there's no user associated with the message.
    // There's a Bot Id, but it's one of those "B012341234" IDs, it's not a bot user id.
    // So what we're going to do here is parse the message text to see if it conforms to a well known workflow
    // step format. If it does, we'll parse out the user that submitted the form and use their id as the user id.
    // This is a hack, but it's the best we can do. - @haacked 2022-07-29
    static readonly Regex WorkflowMessageRegex = new(@"\*.+?\* submission from \u003c@(?<user>.+?)\u003e", RegexOptions.Compiled);
    static string? GetUserIdFromMessageEvent(EventBody messageEvent)
    {
        if (messageEvent.User is { Length: > 0 } user)
        {
            return user;
        }

        if (messageEvent is BotMessageEvent { BotProfile.IsWorkflowBot: true, Text: { Length: > 0 } messageText })
        {
            var match = WorkflowMessageRegex.Match(messageText);
            if (match.Success)
            {
                return match.Groups["user"].Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Constructs an <see cref="MessageEventInfo"/> from a Slack interactive event.
    /// </summary>
    /// <param name="text">The activity text.</param>
    /// <param name="payload">The <see cref="InteractiveMessagePayload"/> received when a button is clicked.</param>
    public static MessageEventInfo? FromSlackInteractiveMessagePayload(
        string text,
        InteractiveMessagePayload payload)
    {
        if (payload.User is null)
        {
            // If we can't determine the user, we need to ignore this message. It's not an event we can handle.
            return null;
        }

        var buttonClick = payload.PayloadActions.Single();
        var arguments = buttonClick.Value ?? string.Empty;
        var wrappedValue = WrappedValue.Parse(payload.CallbackId);
        var interactionInfo = CallbackInfo.TryParse(wrappedValue.ExtraInformation, out var cb)
            ? new MessageInteractionInfo(payload, arguments, cb)
            : null;

        return Create(
            text,
            payload.Channel.Id,
            payload.User.Id,
            payload.MessageTimestamp,
            payload.OriginalMessage.Require().ThreadTimestamp,
            Array.Empty<string>(),
            interactionInfo);
    }

    /// <summary>
    /// Constructs a <see cref="MessageEventInfo"/> from a Slack <see cref="MessageBlockActionsPayload"/>.
    /// </summary>
    /// <param name="text">The activity text.</param>
    /// <param name="payload">The <see cref="InteractiveMessagePayload"/> received when a button is clicked.</param>
    public static MessageEventInfo? FromSlackBlockActionsPayload(string text, MessageBlockActionsPayload payload)
    {
        if (payload.User is null || payload.Actions is { Count: 0 })
        {
            // If we can't determine the user, we need to ignore this message. It's not an event we can handle.
            return null;
        }

        var firstAction = payload.Actions[0];
        var arguments = firstAction switch
        {
            IValueElement valueElement => valueElement.Value,
            IMultiValueElement multiValueElement => string.Join(' ', multiValueElement.Values),
            _ => string.Empty
        };
        var interactionInfo = CallbackInfo.TryGetCallbackInfoPayloadElement<CallbackInfo>(firstAction, out var cb)
            ? new MessageInteractionInfo(payload, arguments ?? string.Empty, cb)
            : null;

        // Reset the block Ids back to what the user specified.
        foreach (var action in payload.Actions)
        {
            if (action.BlockId is not null)
            {
                // TODO: We probably should set action IDs back, but this only applies to user skills
                // so we'll hold off on that when we give them support for block kit interactions.
                action.BlockId = WrappedValue.Parse(action.BlockId).OriginalValue;
            }
        }
        var timestamp = payload.Container.MessageTimestamp;

        return Create(
            text,
            payload.Channel.Id,
            payload.User.Id,
            timestamp,
            payload.Message?.ThreadTimestamp,
            mentionedUserIds: Array.Empty<string>(),
            interactionInfo);
    }

    /// <summary>
    /// Constructs an <see cref="MessageEventInfo"/> from a Slack message action event.
    /// </summary>
    /// <param name="text">The activity text.</param>
    /// <param name="payload">The <see cref="MessageActionPayload"/> received when a message shortcut is clicked.</param>
    public static MessageEventInfo FromSlackMessageActionPayload(
        string text,
        MessageActionPayload payload)
    {
        var interactionInfo = CallbackInfo.TryParse(payload.CallbackId, out var callbackInfo)
            ? new MessageInteractionInfo(payload, string.Empty, callbackInfo)
            : null;

        return Create(
            text,
            payload.Channel.Id,
            payload.User.Require().Id,
            payload.MessageTimestamp,
            payload.Message.ThreadTimestamp,
            Array.Empty<string>(),
            interactionInfo);
    }

    static MessageEventInfo Create(
        string text,
        string platformRoomId,
        string platformUserId,
        string? slackTimestamp,
        string? slackThreadTimestamp,
        IReadOnlyList<string> mentionedUserIds,
        MessageInteractionInfo? interactionInfo,
        bool directMessage = false,
        bool ignore = false,
        IReadOnlyList<ILayoutBlock>? blocks = null,
        IReadOnlyList<FileUpload>? files = null,
        bool workflowMessage = false)
    {
        return new MessageEventInfo(
            text,
            platformRoomId,
            platformUserId,
            mentionedUserIds,
            directMessage,
            ignore,
            slackTimestamp,
            slackThreadTimestamp,
            interactionInfo,
            blocks ?? Array.Empty<ILayoutBlock>(),
            files ?? Array.Empty<FileUpload>(),
            workflowMessage);
    }
}
