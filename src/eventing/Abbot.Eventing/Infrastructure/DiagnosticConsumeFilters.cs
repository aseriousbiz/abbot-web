using System.Diagnostics;
using System.Diagnostics.Metrics;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Eventing.Infrastructure;

// A marker just for logger categories and ILogger<T> extension methods
// ReSharper disable once ClassNeverInstantiated.Global
public class DiagnosticFilters
{
}

/// <summary>
/// Performs diagnostic operations on messages received at any endpoint, even those without a consumer type attached.
/// </summary>
/// <remarks>
/// This filter runs for messages bound for a Consumer, AND those bound for a Saga.
/// The message is opaque at this point, it hasn't been deserialized.
/// </remarks>
public class DiagnosticConsumeFilter : IFilter<ConsumeContext>
{
    readonly IClock _clock;
    readonly ILogger<DiagnosticFilters> _logger;

    public DiagnosticConsumeFilter(IClock clock, ILogger<DiagnosticFilters> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public async Task Send(ConsumeContext context, IPipe<ConsumeContext> next)
    {
        using var messageScope = _logger.BeginMessageScope(
            context.MessageId,
            context.ConversationId,
            context.CorrelationId,
            context.RequestId);

        var latency = _clock.UtcNow - context.SentTime;
        _logger.ConsumingMessage(context.SourceAddress, context.DestinationAddress, (long?)latency?.TotalMilliseconds);

        await next.Send(context);
    }

    public void Probe(ProbeContext context)
    {
    }
}

/// <summary>
/// Performs diagnostic operations on messages received at an endpoint with a consumer type bound.
/// </summary>
/// <remarks>
/// This filter runs for messages bound for a Consumer, BUT NOT those bound for a Saga.
/// </remarks>
public class DiagnosticConsumerConsumeFilter<TConsumer, TMessage> : IFilter<ConsumerConsumeContext<TConsumer, TMessage>>
    where TConsumer : class
    where TMessage : class
{
    readonly IClock _clock;
    readonly ILogger<DiagnosticFilters> _logger;

    public DiagnosticConsumerConsumeFilter(IClock clock, ILogger<DiagnosticFilters> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public async Task Send(ConsumerConsumeContext<TConsumer, TMessage> context, IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next)
    {
        var messageTypeName = GetFriendlyName(context.Message.GetType());
        var consumerTypeName = GetFriendlyName(context.Consumer.GetType());

        using var scope = _logger.BeginConsumerScope(messageTypeName, consumerTypeName);

        // If the message has scope values to give us, create an inner scope for those.
        using var messageScope = (context.Message as IProvidesLoggerScope)?.BeginScope(_logger);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            _logger.ConsumeFault(ex);
            throw;
        }
        finally
        {
            _logger.ConsumedMessage(context.SourceAddress, context.DestinationAddress, stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Shrinks type names to be more readable in dashboards.
    /// It's difficult to do this in Kusto, so we do it here.
    /// </summary>
    /// <param name="t">The type to get the Friendly Name for.</param>
    /// <returns>A shorter, friendlier, type name.</returns>
    internal static string GetFriendlyName(Type t)
    {
        var ns = t.Namespace ?? string.Empty;

        // Strip off "MassTransit.DynamicInternal." from the namespace before shortening
        if (ns.StartsWith("MassTransit.DynamicInternal.", StringComparison.InvariantCulture))
        {
            ns = ns["MassTransit.DynamicInternal.".Length..];
        }

        if (ns.StartsWith("Serious.Abbot", StringComparison.InvariantCulture))
        {
            ns = string.Empty;
        }
        else if (ns.StartsWith("MassTransit.SignalR", StringComparison.InvariantCulture))
        {
            ns = "SignalR:";
        }
        else
        {
            ns = $"{ns}.";
        }

        var name = $"{ns}{t.Name.LeftBefore('`')}";

        if (t.IsGenericType)
        {
            name += $"<{string.Join(", ", t.GetGenericArguments().Select(GetFriendlyName))}>";
        }

        return name;
    }

    public void Probe(ProbeContext context)
    {
    }
}

public class DiagnosticStateMachineObserver<T> : IEventObserver<T>, IStateObserver<T>
    where T : class, ISaga
{
    readonly IClock _clock;
    readonly ILogger<DiagnosticFilters> _logger;

    public DiagnosticStateMachineObserver(IClock clock, ILogger<DiagnosticFilters> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public Task PreExecute(BehaviorContext<T> context) => PreExecuteCore(context);
    public Task PreExecute<T1>(BehaviorContext<T, T1> context) where T1 : class => PreExecuteCore(context);

    Task PreExecuteCore(BehaviorContext<T> context)
    {
        DiagnosticTrackers trackers;
        // We can do better logging if we know it's the Playbook Saga (which is the only saga right now)
        if (context.Saga is PlaybookRun playbookRun)
        {
            var eventScope = _logger.BeginEventScope(
                context.CorrelationId,
                context.Event.Name,
                playbookRun.State);
            var runScope = _logger.BeginPlaybookRunScope(
                playbookRun);
            trackers = new DiagnosticTrackers(eventScope, runScope);
        }
        else
        {
            // Some other saga?
            var eventScope = _logger.BeginEventScope(
                context.CorrelationId,
                context.Event.Name,
                null);
            trackers = new DiagnosticTrackers(eventScope, null);
        }
        context.AddOrUpdatePayload(
            () => trackers,
            _ => trackers);

        _logger.EventOccurred(context.Event.Name);

        return Task.CompletedTask;
    }

    public Task PostExecute(BehaviorContext<T> context)
    {
        _logger.EventComplete(context.Event.Name);
        EventDidFinish(context);
        return Task.CompletedTask;
    }

    public Task PostExecute<T1>(BehaviorContext<T, T1> context) where T1 : class
    {
        _logger.EventComplete(context.Event.Name);
        EventDidFinish(context);
        return Task.CompletedTask;
    }

    public Task ExecuteFault(BehaviorContext<T> context, Exception exception)
    {
        _logger.EventFaulted(exception, context.Event.Name);
        EventDidFinish(context);
        return Task.CompletedTask;
    }

    public Task ExecuteFault<T1>(BehaviorContext<T, T1> context, Exception exception) where T1 : class
    {
        _logger.EventFaulted(exception, context.Event.Name);
        EventDidFinish(context);
        return Task.CompletedTask;
    }

    public Task StateChanged(BehaviorContext<T> context, State currentState, State previousState)
    {
        _logger.StateTransition(currentState.Name, previousState.Name);
        return Task.CompletedTask;
    }

    static void EventDidFinish(BehaviorContext<T> context)
    {
        if (context.TryGetPayload<DiagnosticTrackers>(out var scopes))
        {
            scopes.EventScope?.Dispose();
            scopes.PlaybookRunScope?.Dispose();
        }
    }

    record DiagnosticTrackers(IDisposable? EventScope, IDisposable? PlaybookRunScope);
}

static partial class DiagnosticConsumeFiltersLoggingExtensions
{
    static readonly Func<ILogger, Guid?, Guid?, Guid?, Guid?, IDisposable?> MessageScope =
        LoggerMessage.DefineScope<Guid?, Guid?, Guid?, Guid?>(
            "Message Scope. BusMessageId={BusMessageId}, BusConversationId={BusConversationId}, BusCorrelationId={BusCorrelationId}, BusRequestId={BusRequestId}");

    public static IDisposable? BeginMessageScope(this ILogger<DiagnosticFilters> logger, Guid? messageId, Guid? conversationId, Guid? correlationId, Guid? requestId) => MessageScope(logger, messageId, conversationId, correlationId, requestId);

    static readonly Func<ILogger, string?, string?, IDisposable?> ConsumerScope =
        LoggerMessage.DefineScope<string?, string?>(
            "Consumer Scope. BusMessageType={BusMessageType}, BusConsumerType={BusConsumerType}");

    public static IDisposable? BeginConsumerScope(this ILogger<DiagnosticFilters> logger, string? messageType, string? consumerType) => ConsumerScope(logger, messageType, consumerType);

    static readonly Func<ILogger, Guid?, string?, string?, IDisposable?> EventScope =
        LoggerMessage.DefineScope<Guid?, string?, string?>(
            "Saga Event Scope. CorrelationId: {BusCorrelationId}, Event: {SagaEventName}, State: {SagaState}");

    public static IDisposable? BeginEventScope(this ILogger<DiagnosticFilters> logger, Guid? correlationId, string eventName, string? currentState)
        => EventScope(logger, correlationId, eventName, currentState);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Consuming message sent by {SourceAddress} to {DestinationAddress}. Queue Latency: {QueueLatencyInMilliseconds}")]
    public static partial void ConsumingMessage(this ILogger<DiagnosticFilters> logger, Uri? sourceAddress, Uri? destinationAddress, long? queueLatencyInMilliseconds);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Consumed message sent by {SourceAddress} to {DestinationAddress}. Consume Duration: {ConsumeDurationInMilliseconds}")]
    public static partial void ConsumedMessage(this ILogger<DiagnosticFilters> logger, Uri? sourceAddress, Uri? destinationAddress, long? consumeDurationInMilliseconds);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "Consume fault")]
    public static partial void ConsumeFault(this ILogger<DiagnosticFilters> logger, Exception ex);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Event {SagaEventName} occurred.")]
    public static partial void EventOccurred(this ILogger<DiagnosticFilters> logger, string sagaEventName);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Event {SagaEventName} complete.")]
    public static partial void EventComplete(this ILogger<DiagnosticFilters> logger, string sagaEventName);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Event {SagaEventName} faulted.")]
    public static partial void EventFaulted(this ILogger<DiagnosticFilters> logger, Exception ex, string sagaEventName);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Transitioned from {PreviousState} to {CurrentState}.")]
    public static partial void StateTransition(this ILogger<DiagnosticFilters> logger, string currentState,
        string previousState);
}
