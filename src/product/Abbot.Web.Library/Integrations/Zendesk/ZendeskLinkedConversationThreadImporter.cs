using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;

namespace Serious.Abbot.Integrations.Zendesk;

/// <summary>
/// Used to import a linked conversation from Slack into Zendesk
/// </summary>
public class ZendeskLinkedConversationThreadImporter
{
    static readonly ILogger<ZendeskLinkedConversationThreadImporter> Log = ApplicationLoggerFactory.CreateLogger<ZendeskLinkedConversationThreadImporter>();
    readonly IConversationRepository _conversationRepository;
    readonly ISlackThreadExporter _slackThreadExporter;
    readonly ISlackResolver _slackResolver;
    readonly ISlackToZendeskCommentImporter _commentImporter;

    public ZendeskLinkedConversationThreadImporter(
        IConversationRepository conversationRepository,
        ISlackThreadExporter slackThreadExporter,
        ISlackResolver slackResolver,
        ISlackToZendeskCommentImporter commentImporter)
    {
        _conversationRepository = conversationRepository;
        _slackThreadExporter = slackThreadExporter;
        _slackResolver = slackResolver;
        _commentImporter = commentImporter;
    }

    [Queue(HangfireQueueNames.HighPriority)]
    public async Task ImportThreadAsync(Id<ConversationLink> id, string settingName)
    {
        var linkedConversation = await _conversationRepository.GetConversationLinkAsync(id)
            .Require();

        using var orgScope = Log.BeginOrganizationScope(linkedConversation.Organization);
        using var convoScopes = Log.BeginConversationRoomAndHubScopes(linkedConversation.Conversation);

        var currentOrganization = linkedConversation.Organization;
        var conversation = linkedConversation.Conversation;

        // Get the most recent replies for this message.
        var messages = await _slackThreadExporter.RetrieveMessagesAsync(
            settingName,
            currentOrganization);
        Log.FetchedReplies(messages.Count, settingName);

        // Filter out the root message.
        messages = messages.Where(m => m.Timestamp != conversation.FirstMessageId).ToList();

        if (messages is { Count: 0 })
        {
            // No replies to import.
            return;
        }

        var conversationMessages = await ToConversationMessages(
            messages,
            conversation,
            currentOrganization);

        await _commentImporter.ImportThreadAsync(conversation, conversationMessages);
    }

    async Task<List<ConversationMessage>> ToConversationMessages(
        IEnumerable<SlackMessage> messages,
        Conversation conversation,
        Organization currentOrganization)
    {
        // Filter out bot messages and other "status" messages.
        var list = new List<ConversationMessage>();
        foreach (var message in messages.Where(m => m.SubType is not { Length: > 0 }))
        {
            var sourceTeam = message.SourceTeam;
            var sourceOrganization = sourceTeam is null
                ? currentOrganization
                : await _slackResolver.ResolveOrganizationAsync(sourceTeam, currentOrganization);

            var from = message.User is { Length: > 0 } user
               ? await _slackResolver.ResolveMemberAsync(user, sourceOrganization)
               : null;

            if (from is null)
                continue;

            var timestamp = SlackTimestamp.Parse(message.Timestamp.Require());
            list.Add(new ConversationMessage(
                message.Text ?? string.Empty,
                sourceOrganization,
                from,
                conversation.Room,
                timestamp.UtcDateTime,
                MessageId: message.Timestamp,
                message.ThreadTimestamp,
                message.Blocks,
                message.Files,
                MessageContext: null,
                Deleted: message.IsDeleted()));
        }
        return list;
    }
}

static partial class ZendeskLinkedConversationThreadImporterLoggingExtensions
{
    [LoggerMessage(
        1,
        LogLevel.Information,
        "Fetched {ReplyCount} replies from {SettingName}")]
    public static partial void FetchedReplies(
        this ILogger logger,
        int replyCount,
        string settingName);
}
