using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using MassTransit;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Events;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Routing;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using MessageContext = Serious.Abbot.Messaging.MessageContext;

namespace Serious.Abbot;

/// <summary>
/// This is Abbot. This is the Bot Framework <see cref="ActivityHandler"/> that we register as Abbot and
/// handles incoming chat messages.
/// <remarks>
/// <para>
/// This is an implementation of the <see cref="Microsoft.Bot.Builder.IBot"/> interface. It can handle multiple activity types such
/// as incoming messages, user change events, etc.
/// </para>
/// <para>
/// Normal messages call the <see cref="OnMessageActivityAsync"/> method. This method is also called for button
/// payloads.
/// </para>
/// <para>
/// Team and user events call <see cref="OnEventAsync"/> such as team renames, user timezone changes, etc.
/// </para>
/// <para>
/// When Abbot is installed into a new organization, <see cref="OnInstallationUpdateAddAsync"/> is called. And when
/// it is removed, <see cref="OnInstallationUpdateRemoveAsync"/> is called.
/// </para>
/// <para>
/// When a user opens the Abbot Home in Slack, <see cref="OnConversationUpdateActivityAsync"/> is called.
/// </para>
/// </remarks>
/// </summary>
public class MetaBot : ActivityHandler
{
    static readonly ILogger<MetaBot> Log = ApplicationLoggerFactory.CreateLogger<MetaBot>();

    readonly ITurnContextTranslator _messageTranslator;
    readonly ISkillRouter _skillRouter;
    readonly IMessageClassifier _messageClassifier;
    readonly ITextAnalyticsClient _textAnalyticsClient;
    readonly ConversationMatcher _conversationMatcher;
    readonly ISkillNotFoundHandler _skillNotFoundHandler;
    readonly IAuditLog _auditLog;
    readonly IConversationTracker _conversationTracker;
    readonly IBotInstaller _installer;
    readonly IUrlGenerator _urlGenerator;
    readonly IPublishEndpoint _publishEndpoint;
    readonly FeatureService _featureService;
    readonly Reactor _reactor;
    readonly IClock _clock;

    static readonly Counter<long> MessageCount = AbbotTelemetry.Meter.CreateCounter<long>(
        "abbot.messages.count",
        "messages",
        "Counts messages received by Abbot.");

    public MetaBot(
        ITurnContextTranslator messageTranslator,
        ISkillRouter skillRouter,
        IMessageClassifier messageClassifier,
        ITextAnalyticsClient textAnalyticsClient,
        ConversationMatcher conversationMatcher,
        ISkillNotFoundHandler skillNotFoundHandler,
        IAuditLog auditLog,
        IConversationTracker conversationTracker,
        IBotInstaller installer,
        IUrlGenerator urlGenerator,
        IPublishEndpoint publishEndpoint,
        FeatureService featureService,
        Reactor reactor,
        IClock clock)
    {
        _messageTranslator = messageTranslator;
        _skillRouter = skillRouter;
        _messageClassifier = messageClassifier;
        _textAnalyticsClient = textAnalyticsClient;
        _conversationMatcher = conversationMatcher;
        _skillNotFoundHandler = skillNotFoundHandler;
        _auditLog = auditLog;
        _conversationTracker = conversationTracker;
        _installer = installer;
        _urlGenerator = urlGenerator;
        _publishEndpoint = publishEndpoint;
        _featureService = featureService;
        _reactor = reactor;
        _clock = clock;
    }

    protected override async Task OnMessageActivityAsync(
        ITurnContext<IMessageActivity> turnContext,
        CancellationToken cancellationToken)
    {
        if (turnContext.Activity.Text is null or { Length: 0 } && turnContext.Activity.Value is null)
        {
            // Sometimes Direct Line will send empty messages to make sure the bot is still
            // responding. We should ignore these.
            // https://docs.microsoft.com/en-us/azure/bot-service/rest-api/bot-framework-rest-direct-line-3-0-receive-activities?view=azure-bot-service-4.0#process-messages
            return;
        }

        Log.ReceivedMessage(turnContext.Activity.Id);

        var message = await _messageTranslator.TranslateMessageAsync(turnContext);
        if (message is null)
        {
            return; // Some messages are meant to be ignored.
        }

        if (message.From.IsAbbot())
        {
            // In some unknown circumstances, Abbot can receive messages from itself.
            // Normally these can be filtered out by the Slack adaptor, but in case they don't, we ignore them here.
            return;
        }

        // Now that we've translated the message, start logging scopes with the relevant information.
        using var orgScope = Log.BeginOrganizationScope(message.Organization);
        using var memberScope = Log.BeginMemberScope(message.From);
        using var roomScope = Log.BeginRoomScope(message.Room);

        // This message is something we're going to process, so log it.
        Log.ProcessingMessage();

        // We're going to get a little haacky here until we can unify event and interaction routing.
        var callbackInfo = message.Payload.InteractionInfo?.CallbackInfo;
        await (callbackInfo switch
        {
            InteractionCallbackInfo => HandleInteractionCallbackAsync(message),
            _ => HandleMessageAsync(message, cancellationToken)
        });
    }

    // Handles interactions with UI elements within views or messages.
    async Task HandleInteractionCallbackAsync(IPlatformEvent platformMessage)
    {
        var handler = _skillRouter.RetrievePayloadHandler(platformMessage);
        if (handler != PayloadHandlerRouteResult.Ignore)
        {
            Log.ReceivedInteractionCallback(handler.HandlerInvoker.GetType().FullName);
        }
        await handler.HandlerInvoker.InvokeAsync(platformMessage);
    }

    async Task HandleMessageAsync(IPlatformMessage platformMessage, CancellationToken cancellationToken)
    {
        var metricTags = AbbotTelemetry.CreateOrganizationTags(platformMessage.Organization);
        var result = await _skillRouter.RetrieveSkillAsync(platformMessage);

        void CountMessage(string disposition)
        {
            metricTags.Add("disposition", disposition);
            metricTags.Add("is_conversation", result.Context?.Conversation is not null);
            MessageCount.Add(1, metricTags);
        }

        // Ok, we're going to log message receipt.
        // It could mean a lot of logs, but it's much less than logging every single event.
        if (result == RouteResult.Ignore)
        {
            // If the result doesn't match a command to Abbot, ignore it.
            CountMessage("ignored");
            Log.IgnoredMessage();
            return;
        }

        if (!platformMessage.Organization.Enabled)
        {
            CountMessage("disabled");
            Log.OrganizationDisabled();

            if (platformMessage.ShouldBeHandledByAbbot(result))
            {
                await platformMessage.Responder.SendActivityAsync($"Sorry, I cannot do that. Your organization is disabled. Please contact {WebConstants.SupportEmail} for more information.");
            }

            return;
        }

        var context = result.Context;

        var messageCanChangeConversationState = platformMessage.ShouldTrackMessage(result);

        // This runs AI to categorize the message.
        var message = await CreateFromLiveMessageAsync(context, messageCanChangeConversationState);
        if (await _conversationMatcher.IdentifyConversationAsync(message) is { } conversationMatch)
        {
            context.ConversationMatch = conversationMatch;
        }

        // Always create a conversation scope if the message has an associated conversation
        // This means that logs from skill invocations will be tagged with the conversation ID for the conversation
        // they are in (if any).
        using var scope = context.Conversation is not null
            ? Log.BeginConversationRoomAndHubScopes(context.Conversation)
            : null;

        if (messageCanChangeConversationState)
        {
            var ignoreSocialMessages = platformMessage.Organization.Settings is { IgnoreSocialMessages: true };

            var isSocial = message.Categories.Any(c => c is { Name: "topic", Value: "social" });

            if (context.Conversation is null && !(ignoreSocialMessages && isSocial))
            {
                // This is a message that doesn't have a conversation associated with it, and it's not social.
                // We'll try to create one if the message is from a supportee.
                var conversation = await _conversationTracker.TryCreateNewConversationAsync(message, _clock.UtcNow, context.ConversationMatch?.Result);
                context.ConversationMatch = new ConversationMatch(null, conversation);
            }
            else if (context.Conversation is not null)
            {
                // Update the conversation with the new message, even if the new message is social.
                await _conversationTracker.UpdateConversationAsync(context.Conversation, message);
            }

            // If the message should be handled by Abbot, we want to let it continue.
            if (!platformMessage.ShouldBeHandledByAbbot(result))
            {
                CountMessage("conversation");
                return;
            }
        }

        // Check if we should route this to the Magic Responder
        // We do this before adding the reaction, because we don't want to add a reaction to a message that's going to be processed via Mass Transit.
        var magicResponderEnabled = result.Context.Organization.Settings.AIEnhancementsEnabled == true
                                    && await _featureService.IsEnabledAsync(FeatureFlags.MagicResponder, result.Context);
        if (result.Skill is null && magicResponderEnabled)
        {
            // It is! There's no skill, and so let's fire off a Mass Transit message and be done.
            // If and when we move more of the 👆above logic into consumers, we can move this publish further and further up this method.
            // ReSharper disable once MethodSupportsCancellation
            await _publishEndpoint.Publish(new ReceivedChatMessage
            {
                ChatMessage = ChatMessage.Create(platformMessage, result.Context.Conversation),
            }, cancellationToken);
            CountMessage("magic-responder");
            return;
        }

        // So much await.
        await using var _ = await _reactor.ReactDuringAsync("robot_face", context);
        if (result.Skill is null)
        {
            var isInteraction = platformMessage.Payload.InteractionInfo is not null;
            if (isInteraction)
            {
                CountMessage("interaction");
                return;
            }

            if (platformMessage.DirectMessage)
            {
                CountMessage("abbot-dm");
                await RespondWithOptionsAsync(platformMessage);
            }
            else
            {
                CountMessage("skill-not-found");
                await _skillNotFoundHandler.HandleSkillNotFoundAsync(context);
            }

            return;
        }

        CountMessage("skill");
        Log.InvokingSkill(context.SkillName, context.Arguments.Value);
        await RunAndLogSkillAsync(result.Skill, context, cancellationToken);
    }

    // This might belong in its own service, but we're only doing this here.
    async Task<ConversationMessage> CreateFromLiveMessageAsync(
        MessageContext context,
        bool messageCanChangeConversationState)
    {
        var aiEnhancementsEnabled = context.Organization.Settings is { AIEnhancementsEnabled: true };
        var aiFeatureEnabled = await _featureService.IsEnabledAsync(FeatureFlags.AIEnhancements, context);

        // If a message cannot change the conversation state (such as a message from a bot or a command to call a
        // skill, we don't need to run the AI services.
        if (!aiFeatureEnabled || !aiEnhancementsEnabled || !messageCanChangeConversationState || context.MessageId is null)
        {
            return ConversationMessage.CreateFromLiveMessage(context);
        }

        var sensitiveValues = await _textAnalyticsClient.RecognizePiiEntitiesAsync(context.OriginalMessage);

        var classifierResult = await _messageClassifier.ClassifyMessageAsync(
            context.OriginalMessage,
            sensitiveValues,
            context.MessageId,
            context.Room,
            context.FromMember,
            context.Organization);

        return ConversationMessage.CreateFromLiveMessage(context, sensitiveValues, classifierResult);
    }

    async Task RespondWithOptionsAsync(IPlatformMessage platformMessage)
    {
        var isTopLevel = platformMessage is { IsInThread: false, MessageId.Length: > 0 };

        if (isTopLevel)
        {
            var buttons = new List<IActionElement>
            {
                new ButtonElement("💌 Send feedback")
                {
                    ActionId = InteractionCallbackInfo.For<FeedbackSkill>(),
                    Value = platformMessage.Payload.InteractionInfo?.ResponseUrl?.ToString()
                },
                new ButtonElement("🌐 Take me to Ab.bot", "bye")
                {
                    Url = _urlGenerator.HomePage()
                }
            };

            if (platformMessage is { MessageId: { Length: > 0 } messageId, Room: { } room }
                && platformMessage.From.IsAgent())
            {
                buttons.Insert(0, new ButtonElement("📣 Create announcement")
                {
                    ActionId = InteractionCallbackInfo.For<AnnouncementHandler>(),
                    Value = new AnnouncementHandler.PrivateMetadata(room.PlatformRoomId, messageId)
                });
            }

            var activity = CreateAbbotDmResponseMessage(buttons);

            await platformMessage.Responder.SendActivityAsync(activity, platformMessage.ReplyInThreadMessageTarget);
        }
    }

    internal static RichActivity CreateAbbotDmResponseMessage(
        IEnumerable<IActionElement> buttons,
        string? userChoice = null)
    {
        const string fallbackText = ":wave: Hey! Thanks for the message. What would you like to do next?";

        var blocks = new List<ILayoutBlock>
        {
            new Section(fallbackText)
        };
        if (userChoice is not null)
        {
            blocks.Add(new Context(new MrkdwnText($"You chose to {userChoice}.")));
        }
        else
        {
            blocks.Add(new Actions(buttons.ToArray())
            {
                // This BlockId is only used so we can find the block in our unit tests.
                // The individual buttons have routing info in their ActionIds
                BlockId = "actions-block"
            });
        }

        // Provide the user some options.
        var activity = new RichActivity(fallbackText, blocks.ToArray());
        return activity;
    }

    async Task RunAndLogSkillAsync(
        ISkill skill,
        MessageContext messageContext,
        CancellationToken cancellationToken)
    {
        try
        {
            await skill.OnMessageActivityAsync(messageContext, cancellationToken);
            if (skill is not RemoteSkillCallSkill)
            {
                // RemoteSkillCallSkill logging is handled elsewhere.
                await _auditLog.LogBuiltInSkillRunAsync(skill, messageContext);
            }
        }
        catch (Exception e)
        {
            if (skill is not RemoteSkillCallSkill)
            {
                // RemoteSkillCallSkill logging is handled elsewhere.
                await _auditLog.LogBuiltInSkillRunAsync(skill, messageContext, e);
            }

            throw;
        }
    }

    /// <summary>
    /// Called when we receive events from the chat platform such as a user change event, team rename, etc.
    /// </summary>
    /// <param name="turnContext">The incoming event activity.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    protected override async Task OnEventAsync(
        ITurnContext<IEventActivity> turnContext,
        CancellationToken cancellationToken)
    {
        if (await _messageTranslator.TranslateEventAsync(turnContext) is not { } platformEvent)
        {
            return;
        }

        // Now that we've translated the event, start logging scopes with the relevant information.
        using var orgScope = Log.BeginOrganizationScope(platformEvent.Organization);
        using var memberScope = Log.BeginMemberScope(platformEvent.From);
        using var roomScope = Log.BeginRoomScope(platformEvent.Room);

        // Log something, so that at least we can see we got the message
        // The scopes have all the data we want, but we need to actually log something or that data won't show up in the log.
        Log.ProcessingEvent();

        var result = _skillRouter.RetrievePayloadHandler(platformEvent);
        if (result != PayloadHandlerRouteResult.Ignore)
        {
            await result.HandlerInvoker.InvokeAsync(platformEvent);
        }
    }

    protected override Task OnUnrecognizedActivityTypeAsync(ITurnContext turnContext,
        CancellationToken cancellationToken)
    {
        Log.MethodEntered(typeof(MetaBot), nameof(OnUnrecognizedActivityTypeAsync), "Unrecognized activity");
        return base.OnUnrecognizedActivityTypeAsync(turnContext, cancellationToken);
    }

    protected override async Task OnInstallationUpdateAddAsync(
        ITurnContext<IInstallationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var message = await _messageTranslator.TranslateInstallEventAsync(turnContext);
        await _installer.InstallBotAsync(message);
    }

    protected override async Task OnInstallationUpdateRemoveAsync(
        ITurnContext<IInstallationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var uninstallEvent = await _messageTranslator.TranslateUninstallEventAsync(turnContext);
        if (uninstallEvent is null)
        {
            return;
        }
        await _installer.UninstallBotAsync(uninstallEvent);
    }

    protected override async Task OnConversationUpdateActivityAsync(
        ITurnContext<IConversationUpdateActivity> turnContext,
        CancellationToken cancellationToken)
    {
        var platformEvent = await _messageTranslator.TranslateEventAsync(turnContext);
        if (platformEvent is not null)
        {
            // Now that we've translated the event, start logging scopes with the relevant information.
            using var orgScope = Log.BeginOrganizationScope(platformEvent.Organization);
            using var memberScope = Log.BeginMemberScope(platformEvent.From);
            using var roomScope = Log.BeginRoomScope(platformEvent.Room);

            var result = _skillRouter.RetrievePayloadHandler(platformEvent);
            if (result != PayloadHandlerRouteResult.Ignore && result.HandlerInvoker is { } handler)
            {
                await handler.InvokeAsync(platformEvent);
            }
        }

        await base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
    }
}

static partial class MetaBotLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Processing Event")]
    public static partial void ProcessingEvent(this ILogger<MetaBot> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Received message (ActivityId: {ActivityId})")]
    public static partial void ReceivedMessage(this ILogger<MetaBot> logger, string activityId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "Invoking skill (Name: {SkillName}, Args: {Arguments})")]
    public static partial void InvokingSkill(this ILogger<MetaBot> logger, string skillName, string arguments);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Received interaction callback event, routed to handler {Handler}")]
    public static partial void ReceivedInteractionCallback(this ILogger<MetaBot> logger, string? handler);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Ignored message not intended for Abbot")]
    public static partial void IgnoredMessage(this ILogger<MetaBot> logger);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Processing message")]
    public static partial void ProcessingMessage(this ILogger<MetaBot> logger);
}
