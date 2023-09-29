using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Segment;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Payloads;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Dispatches interactions to the appropriate <see cref="IHandler"/>.
/// </summary>
public interface IHandlerDispatcher
{
    /// <summary>
    /// Handles an interaction with a view.
    /// </summary>
    /// <param name="platformEvent">The incoming view interaction event.</param>
    Task OnViewInteractionAsync(IPlatformEvent<IViewPayload> platformEvent);

    /// <summary>
    /// /Handles an interaction with a message.
    /// </summary>
    /// <param name="platformMessage">The incoming message interaction.</param>
    Task OnMessageInteractionAsync(IPlatformMessage platformMessage);
}

/// <summary>
/// Dispatches interactions to the appropriate <see cref="IHandler"/>.
/// </summary>
public class HandlerDispatcher : IHandlerDispatcher, IPayloadHandlerInvoker
{
    static readonly ILogger<HandlerDispatcher> Log = ApplicationLoggerFactory.CreateLogger<HandlerDispatcher>();

    readonly IHandlerRegistry _handlerRegistry;
    readonly IAnalyticsClient _analyticsClient;

    public HandlerDispatcher(IHandlerRegistry handlerRegistry, IAnalyticsClient analyticsClient)
    {
        _handlerRegistry = handlerRegistry;
        _analyticsClient = analyticsClient;
    }

    public async Task InvokeAsync(IPlatformEvent platformEvent)
    {
        if (!platformEvent.Organization.Enabled)
        {
            Log.OrganizationDisabled();
            return;
        }

        await (platformEvent switch
        {
            IPlatformEvent<IViewPayload> viewInteraction => OnViewInteractionAsync(viewInteraction),
            IPlatformMessage platformMessage => OnMessageInteractionAsync(platformMessage),
            _ => Task.CompletedTask
        });
    }

    async Task OnBlockSuggestionAsync(IHandler handler, IPlatformEvent<BlockSuggestionPayload> platformEvent)
    {
        _analyticsClient.Track(
            "Block Suggestion",
            AnalyticsFeature.Slack,
            platformEvent.From,
            platformEvent.Organization,
            new()
            {
                ["callback_id"] = platformEvent.Payload.View.CallbackId
            });
        var options = await handler.OnBlockSuggestionRequestAsync(platformEvent);
        platformEvent.Responder.SetJsonResponse(options);
    }

    public async Task OnViewInteractionAsync(IPlatformEvent<IViewPayload> platformEvent)
    {
        if (!platformEvent.Organization.Enabled)
        {
            Log.OrganizationDisabled();
            return;
        }

        using var _ = Log.BeginViewInteractionScope(platformEvent.Payload);
        var handler = _handlerRegistry.Retrieve(platformEvent);
        if (handler is null)
        {
            Log.HandlerNotFound();
            // If the result doesn't match a handler, ignore it.
            return;
        }

        Log.HandlerFound(handler.GetType());
        await (platformEvent switch
        {
            IPlatformEvent<IViewBlockActionsPayload> interaction
                => OnInteractionAsync(handler, interaction),
            IPlatformEvent<BlockSuggestionPayload> suggestion
                => OnBlockSuggestionAsync(handler, suggestion),
            IPlatformEvent<IViewSubmissionPayload> submission
                => OnSubmissionAsync(handler, submission),
            IPlatformEvent<IViewClosedPayload> closed
                => OnClosedAsync(handler, closed),
            _ => throw new UnreachableException($"Unexpected platform event type {platformEvent.GetType()}")
        });
    }

    Task OnClosedAsync(IHandler handler, IPlatformEvent<IViewClosedPayload> evt)
    {
        _analyticsClient.Track(
            "View Closed",
            AnalyticsFeature.Slack,
            evt.From,
            evt.Organization,
            new()
            {
                ["callback_id"] = evt.Payload.View.CallbackId
            });
        return handler.OnClosedAsync(new ViewContext<IViewClosedPayload>(evt, handler));
    }

    Task OnSubmissionAsync(IHandler handler, IPlatformEvent<IViewSubmissionPayload> evt)
    {
        _analyticsClient.Track(
            "View Submitted",
            AnalyticsFeature.Slack,
            evt.From,
            evt.Organization,
            new()
            {
                ["callback_id"] = evt.Payload.View.CallbackId
            });
        return handler.OnSubmissionAsync(new ViewContext<IViewSubmissionPayload>(evt, handler));
    }

    Task OnInteractionAsync(IHandler handler, IPlatformEvent<IViewBlockActionsPayload> evt)
    {
        _analyticsClient.Track(
            "View Interaction",
            AnalyticsFeature.Slack,
            evt.From,
            evt.Organization,
            new()
            {
                // We don't put action_id and block_id in the payload right now because they are often dynamically generated
                // That means high cardinality (new value for each interaction) and thus higher costs in the traffic we send to Segment
                // We can revisit this if we find a way to make them static
                ["callback_id"] = evt.Payload.View.CallbackId,
            });
        return handler.OnInteractionAsync(new ViewContext<IViewBlockActionsPayload>(evt, handler));
    }

    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        if (!platformMessage.Organization.Enabled)
        {
            Log.OrganizationDisabled();
            return;
        }

        using var _ = Log.BeginMessageInteractionScope(platformMessage);

        var handler = _handlerRegistry.Retrieve(platformMessage);
        if (handler is null)
        {
            Log.HandlerNotFound();
            // If the result doesn't match a handler, ignore it.
            return;
        }

        Log.HandlerFound(handler.GetType());
        _analyticsClient.Track(
            "Message Interaction",
            AnalyticsFeature.Slack,
            platformMessage.From,
            platformMessage.Organization,
            new()
            {
                ["callback_id"] = (platformMessage.Payload.InteractionInfo?.CallbackInfo.ToString()),
            });
        await handler.OnMessageInteractionAsync(platformMessage);
    }
}

static partial class HandlerDispatcherLoggingExtensions
{
    static readonly Func<ILogger, string?, string?, string?, IDisposable?> MessageScope =
        LoggerMessage.DefineScope<string?, string?, string?>(
            "Message Interaction: {CallbackId} (MessageId={MessageId}, ThreadId={ThreadId})");

    public static IDisposable? BeginMessageInteractionScope(
        this ILogger<HandlerDispatcher> logger,
        IPlatformMessage platformMessage) => MessageScope(logger,
            platformMessage.Payload.InteractionInfo?.CallbackInfo.ToString(),
            platformMessage.MessageId,
            platformMessage.ThreadId);

    static readonly Func<ILogger, Type, string?, string, string?, string?, IDisposable?> ViewScope =
        LoggerMessage.DefineScope<Type, string?, string, string?, string?>(
            "View Interaction: {PayloadType} {CallbackId} (Id={ViewId}, Root={RootViewId}, Previous={PreviousViewId})");

    public static IDisposable? BeginViewInteractionScope(
        this ILogger<HandlerDispatcher> logger,
        IViewPayload payload) => ViewScope(logger,
            payload.GetType(),
            payload.View.CallbackId,
            payload.View.Id,
            payload.View.RootViewId,
            payload.View.PreviousViewId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Handler not found.")]
    public static partial void HandlerNotFound(this ILogger<HandlerDispatcher> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Handler found: {SlackInteractionHandler}.")]
    public static partial void HandlerFound(this ILogger<HandlerDispatcher> logger,
        Type slackInteractionHandler);
}
