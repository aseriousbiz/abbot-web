using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;

namespace Abbot.Common.TestHelpers;

/// <summary>
/// Observes consumption and provides async methods to wait for a message to be consumed.
/// </summary>
public class ConsumerTestObserver : IConsumeObserver
{
    /// <summary>
    /// The default maximum timeout for <see cref="WaitForConsumptionAsync{T}"/>. Defaults to 5 seconds.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    ConcurrentDictionary<Type, ConsumptionTracker> _consumptionTrackers = new();

    Task IConsumeObserver.PreConsume<T>(ConsumeContext<T> context) where T : class =>
        Task.CompletedTask;

    Task IConsumeObserver.PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        var tracker = _consumptionTrackers.GetOrAdd(context.Message.GetType(), t => new ConsumptionTracker(t));
        tracker.Consumed();
        return Task.CompletedTask;
    }

    Task IConsumeObserver.ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        var tracker = _consumptionTrackers.GetOrAdd(context.Message.GetType(), t => new ConsumptionTracker(t));
        tracker.Faulted(exception);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits for a message of type <typeparamref name="TMessage"/> to finish consumption.
    /// </summary>
    /// <param name="timeout">The maximum length of time to wait. Defaults to <see cref="DefaultTimeout"/></param>
    /// <param name="cancellationToken">A cancellation token that will cause the wait to end, when triggered.</param>
    public Task WaitForConsumptionAsync<TMessage>(TimeSpan? timeout = null, CancellationToken cancellationToken = default) where TMessage : class
    {
        timeout ??= DefaultTimeout;
        var tracker = _consumptionTrackers.GetOrAdd(typeof(TMessage), t => new ConsumptionTracker(t));

        // No timeout when debugging.
        if (Debugger.IsAttached)
        {
            return tracker.WasConsumed;
        }

        return tracker.WasConsumed.WaitAsync(timeout.Value, cancellationToken);
    }

    class ConsumptionTracker
    {
        TaskCompletionSource<object?> _tcs = new();
        int _consumptionCount;

        public Type MessageType { get; }
        public Task WasConsumed => _tcs.Task;
        public int ConsumptionCount => _consumptionCount;

        public ConsumptionTracker(Type messageType)
        {
            MessageType = messageType;
        }

        public void Consumed()
        {
            Interlocked.Increment(ref _consumptionCount);
            _tcs.TrySetResult(null);
        }

        public void Faulted(Exception ex)
        {
            Interlocked.Increment(ref _consumptionCount);
            _tcs.TrySetException(ex);
        }
    }
}
