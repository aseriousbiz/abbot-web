using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Infrastructure;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Live;
using Serious.Abbot.Repositories;
using Serious.Slack;

namespace Serious.Abbot.Eventing;

public class AutoSummarizationConsumer : IConsumer<NewMessageInConversation>
{
    readonly Summarizer _summarizer;
    readonly ISanitizedConversationHistoryBuilder _conversationHistoryBuilder;
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly FeatureService _featureService;
    readonly ILogger<AutoSummarizationConsumer> _logger;
    readonly IFlashPublisher _flashPublisher;
    readonly HubMessageRenderer _hubMessageRenderer;
    readonly IClock _clock;

    // This ensures that `NewMessageInConversation` messages are processed serially per conversation id.
    // This means the consumer will only be processing a single message per conversation at a time.
    public class Definition : AbbotConsumerDefinition<AutoSummarizationConsumer>
    {
        public Definition()
        {
            RequireSession("auto-summarization-v2");
        }
    }

    public AutoSummarizationConsumer(
        Summarizer summarizer,
        ISanitizedConversationHistoryBuilder conversationHistoryBuilder,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        FeatureService featureService,
        ILogger<AutoSummarizationConsumer> logger,
        IFlashPublisher flashPublisher,
        HubMessageRenderer hubMessageRenderer,
        IClock clock)
    {
        _summarizer = summarizer;
        _conversationHistoryBuilder = conversationHistoryBuilder;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _featureService = featureService;
        _logger = logger;
        _flashPublisher = flashPublisher;
        _hubMessageRenderer = hubMessageRenderer;
        _clock = clock;
    }

    public async Task Consume(ConsumeContext<NewMessageInConversation> context)
    {
        if (!context.Message.IsLive)
        {
            return;
        }

        await SummarizeConversationAsync(
            context.GetPayload<Organization>(),
            context.Message.ConversationId,
            context.Message.ClassificationResult,
            context.Message.ThreadId ?? context.Message.MessageId);
    }

    async Task SummarizeConversationAsync(
        Organization organization,
        Id<Conversation> conversationId,
        ClassificationResult? classificationResult,
        string? threadId = null)
    {
        if (organization.Settings.AIEnhancementsEnabled is not true)
        {
            return;
        }

        if (!await _featureService.IsEnabledAsync(FeatureFlags.AIEnhancements, organization))
        {
            return;
        }

        var conversation = await _conversationRepository.GetConversationAsync(conversationId);
        if (conversation is null || conversation.OrganizationId != organization.Id)
        {
            _logger.EntityNotFound(conversationId);
            return;
        }
        using var convoScope = _logger.BeginConversationRoomAndHubScopes(conversation);

        var history = await _conversationHistoryBuilder.BuildHistoryAsync(conversation);

        if (history is null or { Messages: [] or [.., { SummaryInfo: not null }] })
        {
            return;
        }

        var abbot = await _userRepository.EnsureAbbotMemberAsync(organization);

        var summaryResult = await SummarizeConversationAsync(
            history,
            conversation,
            abbot,
            organization);
        if (summaryResult is null)
        {
            _logger.NullConversationSummarization(conversation);
            return;
        }

        // Update the history with the summary.
        var properties = conversation.Properties with
        {
            Summary = summaryResult.Summary,
            Conclusion = summaryResult.Directives.FirstOrDefault(d => d.Name == "conclusion")?.RawArguments,
            SuggestedState = classificationResult?.Categories.FirstOrDefault(c => c.Name == "state")?.Value
        };

        await _conversationRepository.UpdateSummaryAsync(
            conversation,
            threadId ?? conversation.FirstMessageId,
            summaryResult,
            properties,
            history.Messages[^1].Timestamp,
            abbot,
            _clock.UtcNow);

        // Notify viewers that the conversation list has changed
        await _flashPublisher.PublishAsync(
            FlashName.ConversationListUpdated,
            FlashGroup.Organization(organization));

        // Refresh the Hub message, if any
        await _hubMessageRenderer.UpdateHubMessageAsync(conversation);
    }

    async Task<SummarizationResult?> SummarizeConversationAsync(
        SanitizedConversationHistory history,
        Conversation conversation,
        Member abbot,
        Organization organization)
    {
        try
        {
            return await _summarizer.SummarizeConversationAsync(
                history,
                conversation,
                abbot,
                organization);
        }
        catch (HttpRequestException e)
        {
            _logger.ExceptionWhileSummarizingConversation(e);
            return null;
        }
    }
}

static partial class AutoSummarizationConsumerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Starting Auto Summarization of {ThreadId} at {Watermark}")]
    public static partial void StartingSummarizationThreadSync(
        this ILogger<AutoSummarizationConsumer> logger,
        string threadId,
        SlackTimestamp? watermark);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Slack error retrieving conversation replies: {SlackError}")]
    public static partial void SlackErrorRetrievingReplies(
        this ILogger<AutoSummarizationConsumer> logger,
        string slackError);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "No replies to summarize for {SlackTimestamp}")]
    public static partial void NoRepliesToSummarize(
        this ILogger<AutoSummarizationConsumer> logger, SlackTimestamp? slackTimestamp);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "Conversation Summarization returned a null result for {ConversationId}")]
    public static partial void NullConversationSummarization(
        this ILogger<AutoSummarizationConsumer> logger, Id<Conversation> conversationId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Warning,
        Message = "Signal Summarization returned a null result for {SlackTimestamp}")]
    public static partial void NullSignalSummarization(
        this ILogger<AutoSummarizationConsumer> logger, SlackTimestamp? slackTimestamp);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "HTTP Exception while summarizing conversation.")]
    public static partial void ExceptionWhileSummarizingConversation(
        this ILogger<AutoSummarizationConsumer> logger, HttpRequestException httpException);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Error,
        Message = "HTTP Exception while summarizing intent.")]
    public static partial void ExceptionWhileSummarizingIntent(
        this ILogger<AutoSummarizationConsumer> logger, HttpRequestException httpException);
}
