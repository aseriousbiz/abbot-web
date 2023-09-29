using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;
using Serious.Payloads;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Retrieves the <see cref="IPayloadHandlerInvoker"/> for the <see cref="IPayloadHandler{TPayload}"/> that should
/// respond to the incoming view event. The <see cref="IPayloadHandler{TPayload}"/> is chosen based on the type
/// of the <see cref="IPlatformEvent"/> payload.
/// </summary>
public interface IPayloadHandlerRegistry
{
    /// <summary>
    /// Retrieves the <see cref="IPayloadHandlerInvoker"/> for the <see cref="IPayloadHandler{TPayload}"/> based
    /// on the payload type.
    /// </summary>
    /// <param name="platformEvent">The incoming platform event.</param>
    /// <returns>The <see cref="IPayloadHandlerInvoker"/> that calls the <see cref="IPayloadHandler{TPayload}"/>.</returns>
    IPayloadHandlerInvoker? Retrieve(IPlatformEvent platformEvent);
}

public class PayloadHandlerRegistry : IPayloadHandlerRegistry
{
    readonly IServiceProvider _serviceProvider;

    public PayloadHandlerRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IPayloadHandlerInvoker? Retrieve(IPlatformEvent platformEvent)
    {
        // Need a special case for interactions with UI elements in views or messages. The registered implementation
        // for IHandlerDispatcher is also a payload handler.
        if (platformEvent is IPlatformEvent<IViewPayload>
            or IPlatformEvent<BlockSuggestionPayload>
            or IPlatformMessage)
        {
            if (_serviceProvider.GetService(typeof(IHandlerDispatcher)) is IPayloadHandlerInvoker handlerDispatcher)
            {
                return handlerDispatcher;
            }
        }

        var typeArguments = platformEvent.GetType().GenericTypeArguments;
        var payloadHandlerType = typeof(IPayloadHandler<>).MakeGenericType(typeArguments);
        var payloadHandler = _serviceProvider.GetService(payloadHandlerType);
        if (payloadHandler is null)
        {
            return null;
        }

        var payloadInvokerType = typeof(PayloadHandlerInvoker<>).MakeGenericType(typeArguments);
        return _serviceProvider.Activate(payloadInvokerType, payloadHandler) as IPayloadHandlerInvoker;
    }
}
