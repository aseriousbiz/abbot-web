using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Models;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Signals;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

namespace Serious.Abbot.Integrations.Zendesk;

/// <summary>
/// Used to import Zendesk comments into Slack.
/// </summary>
public interface IZendeskToSlackImporter
{
    /// <summary>
    /// Enqueues a job to grab new comments in Zendesk for a linked ticket and post them to Slack.
    /// </summary>
    /// <param name="organization">The organization the ticket belongs to.</param>
    /// <param name="ticketLink">The link to the Zendesk ticket.</param>
    /// <param name="ticketStatus">The new ticket status.</param>
    /// <param name="zendeskUserId">The Zendesk-specific Id of the Zendesk user updating posting the comment that caused this import.</param>
    void QueueZendeskCommentImport(
        Organization organization,
        ZendeskTicketLink ticketLink,
        string? ticketStatus,
        long? zendeskUserId);
}

public class ZendeskToSlackImporter : IZendeskToSlackImporter
{
    public static readonly string CommentMarkerSettingName = "ZendeskCommentMarker";

    // The maximum allowed by Zendesk is 100.
    const int CommentPageSize = 100;

    readonly ISettingsManager _settingsManager;
    readonly IUserRepository _userRepository;
    readonly ISlackApiClient _slackApiClient;
    readonly IConversationRepository _conversationRepository;
    readonly IBackgroundJobClient _backgroundJobClient;
    readonly IOrganizationRepository _organizationRepository;
    readonly IIntegrationRepository _integrationRepository;
    readonly IZendeskClientFactory _clientFactory;
    readonly IZendeskResolver _zendeskResolver;
    readonly ITextAnalyticsClient _textAnalyticsClient;
    readonly ISystemSignaler _systemSignaler;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;
    readonly ILogger<ZendeskToSlackImporter> _log;

    public ZendeskToSlackImporter(
        ISettingsManager settingsManager,
        IUserRepository userRepository,
        ISlackApiClient slackApiClient,
        IConversationRepository conversationRepository,
        IBackgroundJobClient backgroundJobClient,
        IOrganizationRepository organizationRepository,
        IIntegrationRepository integrationRepository,
        IZendeskClientFactory clientFactory,
        IZendeskResolver zendeskResolver,
        ITextAnalyticsClient textAnalyticsClient,
        ISystemSignaler systemSignaler,
        PlaybookDispatcher playbookDispatcher,
        IAnalyticsClient analyticsClient,
        IClock clock,
        ILogger<ZendeskToSlackImporter> log)
    {
        _settingsManager = settingsManager;
        _userRepository = userRepository;
        _slackApiClient = slackApiClient;
        _conversationRepository = conversationRepository;
        _backgroundJobClient = backgroundJobClient;
        _organizationRepository = organizationRepository;
        _integrationRepository = integrationRepository;
        _clientFactory = clientFactory;
        _zendeskResolver = zendeskResolver;
        _textAnalyticsClient = textAnalyticsClient;
        _systemSignaler = systemSignaler;
        _playbookDispatcher = playbookDispatcher;
        _analyticsClient = analyticsClient;
        _clock = clock;
        _log = log;
    }

    public void QueueZendeskCommentImport(
        Organization organization,
        ZendeskTicketLink ticketLink,
        string? ticketStatus,
        long? zendeskUserId)
    {
        if (!organization.Enabled)
        {
            _log.OrganizationDisabled();
            return;
        }

        _backgroundJobClient.Enqueue(() => ZendeskCommentSyncJob(
            organization.Id,
            ticketLink.ApiUrl.ToString(),
            ticketStatus,
            zendeskUserId,
            /*performContext: Gets filled in by Hangfire */ null!));
    }

    async Task UpdateConversationStateFromTicketStatusAsync(
        IZendeskClient client,
        Conversation conversation,
        ZendeskTicketLink ticketLink,
        string ticketStatus,
        long zendeskUserId)
    {
        var organization = conversation.Organization;

        if (ticketStatus is "Solved" or "Closed" && conversation.State.IsOpen())
        {
            var userLink = new ZendeskUserLink(ticketLink.Subdomain, zendeskUserId);
            var author = await _zendeskResolver.ResolveSlackMessageAuthorAsync(client, organization, userLink);
            var actor = author.Member ?? await _userRepository.EnsureAbbotMemberAsync(organization);
            await _conversationRepository.CloseAsync(conversation, actor, _clock.UtcNow, "ticket");
        }

        var ticketLinkWithStatus = ticketLink with
        {
            Status = ticketStatus
        };

        _systemSignaler.EnqueueSystemSignal(
            SystemSignal.TicketStateChangedSignal,
            arguments: ticketLinkWithStatus.ToJson(),
            organization,
            conversation.Room.ToPlatformRoom(),
            conversation.StartedBy,
            MessageInfo.FromConversation(conversation));

        var outputs = new OutputsBuilder()
            .SetConversation(conversation)
            .SetTicketLink(ticketLinkWithStatus)
            .Outputs;

        await _playbookDispatcher.DispatchAsync(
            ZendeskTicketLinkStatusChangedTrigger.Id,
            outputs,
            organization,
            PlaybookRunRelatedEntities.From(conversation));
    }

    // This is for any legacy jobs that might be queued up and about to run.
    [AutomaticRetry(Attempts = 0)]
    public async Task ZendeskCommentSyncJob(
        int organizationId,
        string ticketUrl,
        PerformContext performContext) => await ZendeskCommentSyncJob(
            organizationId,
            ticketUrl,
            null,
            null,
            performContext);

    [AutomaticRetry(Attempts = 0)]
    [Queue(HangfireQueueNames.HighPriority)]
    public async Task ZendeskCommentSyncJob(
        int organizationId,
        string ticketUrl,
        string? ticketStatus,
        long? zendeskUserId,
        PerformContext performContext)
    {
        // We can't run at the same time as another job for the same organization/ticket
        // I sure hope we don't actually have to wait for this lock, because it uses Thread.Sleep and that makes me feel bad.
        var lockKey = $"{nameof(ZendeskToSlackImporter)}:{organizationId}:{ticketUrl}";
        using var jobLock = _log.LogElapsed(
            $"{nameof(ZendeskCommentSyncJob)}.AcquireDistributedLock",
            () => performContext.Connection.AcquireDistributedLock(lockKey, TimeSpan.FromSeconds(10)));

        var organization = await _organizationRepository.GetAsync(organizationId).Require();

        using var _ = _log.BeginOrganizationScope(organization);
        if (!organization.Enabled)
        {
            // We don't do things for disabled orgs.
            _log.OrganizationDisabled();
            return;
        }

        if (!organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            // No token! Bot must have been uninstalled.
            // Log and bail, but don't throw
            _log.OrganizationHasNoSlackApiToken();
            return;
        }

        var (_, settings) =
            await _integrationRepository.GetIntegrationAsync<ZendeskSettings>(organization);

        var ticket = ZendeskTicketLink.Parse(ticketUrl).Require();

        // Validate a few preconditions
        Expect.True(settings is { HasApiCredentials: true });
        Expect.True(string.Equals(settings.Subdomain, ticket.Subdomain, StringComparison.OrdinalIgnoreCase));

        // Go find the linked conversation
        var externalId = ticket.ApiUrl.ToString();
        var conversationLink = await _conversationRepository.GetConversationLinkAsync(
            organization,
            ConversationLinkType.ZendeskTicket,
            externalId);

        var conversation = conversationLink?.Conversation;
        if (conversation is null)
        {
            _log.NoConversationForTicket(externalId);
            return;
        }
        using var convoScope = _log.BeginConversationRoomAndHubScopes(conversation);

        Expect.NotNull(conversation.StartedBy);
        Expect.NotNull(conversation.Room.Organization);
        Expect.NotNull(conversation.Room.Assignments);

        var client = _clientFactory.CreateClient(settings);

        var abbot = await _userRepository.EnsureAbbotMemberAsync(conversation.Organization);
        await SyncZendeskCommentsToSlackAsync(apiToken, client, conversation, organization, ticket, abbot);

        // Update the conversation state from the ticket status.
        // Yes, there's a race condition here. Someone could post another comment with a status change. Well
        // that'll run after this import, so we'll let that one win.
        // The other race condition is someone could change the status of the ticket without a comment. That one
        // could run first and then get overwritten by this one. We'll have to be OK with that as it should be rare.
        // This currently only changes the conversation state if the Ticket is Solved or Closed.
        // It should be rare that a Solved ticket is immediately re-opened. And it's impossible for a Closed ticket to
        // be re-opened (Zendesk creates a new ticket).
        if (ticketStatus != null
            && zendeskUserId is { } userId
            && await _settingsManager.GetTicketStatusAsync(conversation) != ticketStatus)
        {
            await UpdateConversationStateFromTicketStatusAsync(client, conversation, ticket, ticketStatus, userId);
            await _settingsManager.SetTicketStatusAsync(conversation, ticketStatus, abbot);
        }
    }

    async Task SyncZendeskCommentsToSlackAsync(
        string apiToken,
        IZendeskClient client,
        Conversation conversation,
        Organization organization,
        ZendeskTicketLink ticket,
        Member abbot)
    {
        var scope = SettingsScope.Conversation(conversation);

        // Fetch the comment marker if we have one
        var commentMarker = await _settingsManager.GetAsync(scope, CommentMarkerSettingName);

        var after = commentMarker?.Value;
        var hasMore = true;
        CommentListMessage page = null!;
        while (hasMore)
        {
            // Fetch a page of comments
            page = await client.ListTicketCommentsAsync(ticket.TicketId, CommentPageSize, after);

            foreach (var comment in page.Body.Require())
            {
                try
                {
                    if (IsAbbotComment(comment))
                    {
                        // If we post this, we could create a time paradox, which would be bad.
                        _log.SkippingComment(comment.AuditId, comment.Id);
                    }
                    else if (comment.Public is true)
                    {
                        var success = await SyncCommentToSlackAsync(client,
                            apiToken,
                            abbot,
                            comment,
                            new ZendeskUserLink(ticket.Subdomain, comment.AuthorId),
                            conversation,
                            ticket);

                        if (!success)
                        {
                            // Try again without attachments.
                            success = await SyncCommentToSlackAsync(client,
                                apiToken,
                                abbot,
                                comment,
                                new ZendeskUserLink(ticket.Subdomain, comment.AuthorId),
                                conversation,
                                ticket,
                                includeAttachments: false);
                        }

                        _analyticsClient.Track(
                            "Integration Comment Synced",
                            AnalyticsFeature.Integrations,
                            abbot,
                            organization,
                            new {
                                integration = IntegrationType.Zendesk.ToString(),
                                success,
                            });
                    }
                }
                catch (Exception ex)
                {
                    // If we fail to sync a comment, don't keep thrashing it.
                    // We can add retry logic later.
                    _log.ExceptionSyncingComment(ex, comment.AuditId, comment.Id);
                }
            }

            var meta = page.Meta.Require();
            hasMore = meta.HasMore;
            after = meta.AfterCursor;
        }

        // Write the new comment marker, if we have one. The reason we might not have one is if there are no comments.
        if (after is not null)
        {
            await _settingsManager.SetAsync(scope, CommentMarkerSettingName, after, abbot.User);
        }
        else
        {
            Expect.True(page.Body is not { Count: > 0 },
                "After is null, but there were comments.");
        }
    }

    async Task<bool> SyncCommentToSlackAsync(
        IZendeskClient client,
        string apiToken,
        Member abbot,
        Comment comment,
        ZendeskUserLink author,
        Conversation conversation,
        ZendeskTicketLink ticket,
        bool includeAttachments = true)
    {
        var slackAuthor = await _zendeskResolver.ResolveSlackMessageAuthorAsync(client, conversation.Organization, author);

        var blocks = new List<ILayoutBlock>();
        if (comment.HtmlBody is { Length: > 0 } commentHtml)
        {
            blocks.AddRange(ZendeskHtmlParser.ParseHtml(commentHtml));
        }
        else
        {
            blocks.Add(new Section(new MrkdwnText(comment.Body ?? string.Empty)));
        }

        if (includeAttachments)
        {
            var imageAttachments = comment
                .Attachments
                .Where(IsSlackSupportedImage)
                .ToList();

            var imageBlocks = imageAttachments
                .Select(AttachmentToImage)
                .WhereNotNull();

            blocks.AddRange(imageBlocks);

            var files = comment.Attachments
                .Except(imageAttachments)
                .Select(AttachmentToMrkdwnText)
                .WhereNotNull()
                .ToArray();

            if (files.Any())
            {
                blocks.Add(new Section(files)
                {
                    Text = new MrkdwnText("*File Attachments*")
                });
            }
        }

        blocks.Add(new Context(new MrkdwnText($"This comment was posted on the <{ticket.WebUrl}|linked Zendesk ticket>.")));

        var message = new MessageRequest(Channel: conversation.Room.PlatformRoomId, Text: comment.Body)
        {
            ThreadTs = conversation.FirstMessageId,
            UserName = slackAuthor.Member is null ? $"{slackAuthor.DisplayName} (from Zendesk)" : slackAuthor.DisplayName,
            IconUrl = slackAuthor.AvatarUrl is null ? null : new(slackAuthor.AvatarUrl),
            Blocks = blocks
        };

        var response = await _slackApiClient.PostMessageWithRetryAsync(apiToken, message);

        if (response.Ok)
        {
            _log.SyncedComment(comment.AuditId, comment.Id, response.Body.Timestamp ?? "(null)");
            var messageId = SlackTimestamp.Parse(response.Body.Timestamp.Require());

            var sensitiveValues = await _textAnalyticsClient.RecognizePiiEntitiesAsync(comment.Body ?? "");

            var messagePostedEvent = new MessagePostedEvent
            {
                MessageId = response.Body.Timestamp,
                MessageUrl = SlackFormatter.MessageUrl(conversation.Organization.Domain,
                    conversation.Room.PlatformRoomId,
                    response.Body.Timestamp,
                    response.Body.ThreadTimestamp),
                ExternalSource = "Zendesk",
                ExternalMessageId = ticket.ApiUrl.ToString(),
                ExternalAuthorId = new ZendeskUserLink(ticket.Subdomain, comment.AuthorId).ApiUrl.ToString(),
                ExternalAuthor = slackAuthor.DisplayName,
                Metadata = new MessagePostedMetadata
                {
                    Categories = Array.Empty<Category>(),
                    Text = comment.Body,
                    SensitiveValues = sensitiveValues,
                }.ToJson(),
            };

            var isSupportee = slackAuthor.Member is null
                || ConversationTracker.IsSupportee(slackAuthor.Member, conversation.Room);
            await _conversationRepository.UpdateForNewMessageAsync(
                conversation,
                messagePostedEvent,
                new ConversationMessage(
                    message.Text ?? "",
                    conversation.Organization,
                    slackAuthor.Member ?? abbot,
                    conversation.Room,
                    messageId.UtcDateTime,
                    response.Body.Timestamp,
                    conversation.FirstMessageId,
                    blocks,
                    Array.Empty<FileUpload>(),
                    MessageContext: null),
                isSupportee);

            return true;
        }
        else
        {
            _log.ErrorPostingSlackComment(includeAttachments, response.ToString());
            return false;
        }
    }

    public static bool IsSlackSupportedImage(Attachment attachment)
    {
        return attachment.ContentUrl is not null
            && attachment.ContentUrl.Length <= Image.ImageUrlMaxLength
            && attachment.Size <= Image.MaxUploadSize
            && attachment.ContentType is { Length: > 0 } and ("image/png" or "image/jpeg" or "image/gif");
    }

    public static Image? AttachmentToImage(Attachment attachment)
    {
        if (attachment.ContentUrl is null)
        {
            // As far as we know, this should never happen.
            return null;
        }
        var fileName = attachment.FileName ?? "?";
        var title = fileName.TruncateToLength(Image.TitleMaxLength);
        var altText = fileName.TruncateToLength(Image.AltTextMaxLength);
        return new Image(attachment.ContentUrl, altText, title);
    }

    public static TextObject? AttachmentToMrkdwnText(Attachment attachment)
    {
        if (attachment.ContentUrl is null)
        {
            // As far as we know, this should never happen.
            return null;
        }

        var fileName = attachment.FileName ?? "?";
        var contentUrlLength = attachment.ContentUrl.Length;
        var mrkdwnLength = fileName.Length + contentUrlLength + 3; // The 3 accounts for the <|> characters.
        if (mrkdwnLength > Section.MaxFieldLength)
        {
            // Let's do some truncating.
            var maxFileNameLength = Section.MaxFieldLength - attachment.ContentUrl.Length - 3;
            if (maxFileNameLength < 1)
            {
                return null;
            }

            fileName = fileName.TruncateToLength(maxFileNameLength);
        }

        return new MrkdwnText($"<{attachment.ContentUrl}|{fileName}>");
    }

    static bool IsAbbotComment(Comment comment)
    {
        var userAgent = comment.AuditMetadata?.System?.Client;
        if (userAgent is not { Length: > 0 })
        {
            // No User-Agent was provided
            return false;
        }

        var splat = userAgent.Split('/');
        if (splat.Length != 2)
        {
            // Not our User-Agent
            return false;
        }

        // Check if the product name was us.
        return splat[0] == ZendeskClientFactory.UserAgentProductName;
    }
}

public static class ZendeskSettingsExtensions
{
    public static readonly string TicketStatusSettingName = "ZendeskTicketStatus";

    public static async Task<string?> GetTicketStatusAsync(this ISettingsManager settingsManager, Conversation conversation)
    {
        return (await settingsManager.GetAsync(SettingsScope.Conversation(conversation), TicketStatusSettingName))
            ?.Value;
    }

    public static async Task SetTicketStatusAsync(
        this ISettingsManager settingsManager,
        Conversation conversation,
        string status,
        Member actor)
    {
        await settingsManager.SetAsync(
            SettingsScope.Conversation(conversation),
            TicketStatusSettingName,
            status,
            actor.User);
    }
}

static partial class ZendeskToSlackImporterLoggerExtensions
{
    static readonly Func<ILogger, long, int, IDisposable?> SyncingCommentsScope =
        LoggerMessage.DefineScope<long, int>("Syncing comments. TicketId={TicketId}, ConversationId={ConversationId}");

    public static IDisposable? BeginSyncingComments(this ILogger<ZendeskToSlackImporter> logger, long ticketId,
        int conversationId) =>
        SyncingCommentsScope(logger, ticketId, conversationId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Skipping comment {ZendeskAuditId}/{ZendeskCommentId} because it was posted by Abbot.")]
    public static partial void SkippingComment(this ILogger<ZendeskToSlackImporter> logger, long zendeskAuditId,
        long zendeskCommentId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "No conversation associated with ticket {ZendeskTicketUrl}")]
    public static partial void NoConversationForTicket(this ILogger<ZendeskToSlackImporter> logger,
        string zendeskTicketUrl);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Error posting Slack comment (IncludeAttachments: {IncludeAttachments}): {SlackError}")]
    public static partial void ErrorPostingSlackComment(
        this ILogger<ZendeskToSlackImporter> logger,
        bool includeAttachments,
        string? slackError);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Exception syncing comment {ZendeskAuditId}/{ZendeskCommentId}, skipping it.")]
    public static partial void ExceptionSyncingComment(this ILogger<ZendeskToSlackImporter> logger, Exception ex,
        long zendeskAuditId, long zendeskCommentId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Synced comment {ZendeskAuditId}/{ZendeskCommentId} to Slack as message {SyncedMessageId}.")]
    public static partial void SyncedComment(this ILogger<ZendeskToSlackImporter> logger,
        long zendeskAuditId,
        long zendeskCommentId,
        string syncedMessageId);
}
