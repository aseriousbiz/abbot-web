using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;
using Serious.Tasks;

namespace Serious.Abbot.Conversations;

/// <summary>
/// Used to query and resolve the messages in a thread to later be "imported" when creating a
/// <see cref="Conversation"/>.
/// </summary>
public interface IConversationThreadResolver
{
    /// <summary>
    /// Attempts to create <see cref="ConversationMessage"/> instances for all the messages from a conversation thread.
    /// Throws <see cref="System.InvalidOperationException"/> if the conversation cannot be resolved.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> to import the conversation from.</param>
    /// <param name="messageId">The ID of the message within that room to use as the root message.</param>
    /// <returns>A list of <see cref="ConversationMessage"/>s representing the messages found in the new conversation.</returns>
    Task<IReadOnlyList<ConversationMessage>> ResolveConversationMessagesAsync(Room room, string messageId);
}

public class ConversationThreadResolver : IConversationThreadResolver
{
    readonly IConversationsApiClient _conversationsApiClient;
    readonly ISlackResolver _slackResolver;

    public ConversationThreadResolver(IConversationsApiClient conversationsApiClient, ISlackResolver slackResolver)
    {
        _conversationsApiClient = conversationsApiClient;
        _slackResolver = slackResolver;
    }

    public async Task<IReadOnlyList<ConversationMessage>> ResolveConversationMessagesAsync(Room room, string messageId)
    {
        // Make sure we have an api token
        if (!room.Organization.TryGetUnprotectedApiToken(out var token))
        {
            throw new InvalidOperationException("Cannot import message, organization has no API token.");
        }

        if (room.BotIsMember != true)
        {
            throw new InvalidOperationException("Cannot import message, bot is not known to be a member.");
        }

        // Now fetch the entire thread.
        var repliesResponse = await _conversationsApiClient.GetConversationRepliesAsync(
            token,
            channel: room.PlatformRoomId,
            ts: messageId);

        if (!repliesResponse.Ok)
        {
            throw new InvalidOperationException($"Failed to import messages from Slack: {repliesResponse}");
        }

        if (repliesResponse.Body.Count == 0)
        {
            throw new InvalidOperationException($"Failed to import messages from Slack: No messages returned");
        }

        // Create messages for each reply
        return await repliesResponse
            .Body
            .SelectFunc(reply => CreateConversationMessageInstanceAsync(room, reply))
            .WhenAllOneAtATimeAsync();
    }

    async Task<ConversationMessage?> CreateConversationMessageInstanceAsync(Room room, SlackMessage message)
    {
        if (message.User is null)
        {
            // This happens when the message is a bot_message for example.
            return null;
        }
        var member = await _slackResolver.ResolveMemberAsync(message.User, room.Organization);
        if (member is null)
        {
            // Logging handled by ResolveMemberAsync
            return null;
        }

        var timestamp = SlackFormatter.GetDateFromSlackTimestamp(message.Timestamp.Require());

        return new(
            message.Text ?? string.Empty, // As far as we know, message.Text is never null.
            room.Organization,
            member,
            room,
            timestamp,
            message.Timestamp,
            message.ThreadTimestamp,
            message.Blocks,
            message.Files,
            MessageContext: null,
            message.IsDeleted());
    }
}
