using System.Threading.Tasks;
using Serious.Abbot.Events;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Translates an <see cref="IPlatformEvent" /> into a <see cref="IPlatformEvent{TPayload}" />
/// in order to call a strongly-typed <see cref="IPayloadHandler{TPayload}"/>.
/// </summary>
public interface IPayloadHandlerInvoker
{
    /// <summary>
    /// Calls the <see cref="IPayloadHandler{TPayload}"/> with the given <see cref="IPlatformEvent"/>.
    /// </summary>
    /// <param name="platformEvent">The incoming platform event.</param>
    Task InvokeAsync(IPlatformEvent platformEvent);
}

/// <summary>
/// Translates an <see cref="IPlatformEvent" /> into a <see cref="IPlatformEvent{TPayload}" />
/// in order to call a strongly-typed <see cref="IPayloadHandler{TPayload}"/>.
/// </summary>
public class PayloadHandlerInvoker<TPayload> : IPayloadHandlerInvoker
{
    public IPayloadHandler<TPayload> PayloadHandler { get; }

    public PayloadHandlerInvoker(IPayloadHandler<TPayload> payloadHandler)
    {
        PayloadHandler = payloadHandler;
    }

    /// <summary>
    /// Calls the <see cref="IPayloadHandler{TPayload}.OnPlatformEventAsync"/> method with the given
    /// <see cref="IPlatformEvent"/>.
    /// </summary>
    /// <param name="platformEvent">The incoming platform event.</param>
    public Task InvokeAsync(IPlatformEvent platformEvent)
    {
        if (platformEvent is IPlatformEvent<TPayload> typedEvent)
        {
            return PayloadHandler.OnPlatformEventAsync(typedEvent);
        }

        return Task.CompletedTask;
    }
}
