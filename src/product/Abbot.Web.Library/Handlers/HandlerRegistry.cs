using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Retrieves the <see cref="IHandler"/> that should respond to the incoming
/// view event.
/// </summary>
public interface IHandlerRegistry
{
    /// <summary>
    /// Retrieves a <see cref="IHandler"/> from the incoming view interaction.
    /// </summary>
    /// <param name="callbackInfo">The callback info for the incoming event or message.</param>
    /// <returns>The <see cref="IHandler"/> that should handle this event, or null if none is found.</returns>
    IHandler? Retrieve(InteractionCallbackInfo callbackInfo);
}

/// <summary>
/// Retrieves the <see cref="IHandler"/> that should respond to the incoming message or event.
/// </summary>
public class HandlerRegistry : IHandlerRegistry
{
    readonly IReadOnlyDictionary<string, IHandler> _handlers;

    public HandlerRegistry(IEnumerable<IHandler> handlers)
    {
        _handlers = handlers.ToReadOnlyDictionary(handler => handler.GetType().Name, handler => handler);
    }

    public IHandler? Retrieve(InteractionCallbackInfo callbackInfo)
    {
        return _handlers.TryGetValue(callbackInfo.TypeName, out var handler)
            ? handler
            : null;
    }
}

/// <summary>
/// Retrieves the <see cref="IHandler"/> that should respond to the incoming
/// view event.
/// </summary>
public static class HandlerRegistryExtensions
{
    /// <summary>
    /// Retrieves a <see cref="IHandler"/> from the incoming view interaction.
    /// </summary>
    /// <param name="handlerRegistry">The <see cref="IHandlerRegistry"/> this method extends.</param>
    /// <param name="platformEvent">The incoming view interaction event.</param>
    /// <returns>The <see cref="IHandler"/> that should handle this event, or null if none is found.</returns>
    public static IHandler? Retrieve(this IHandlerRegistry handlerRegistry, IPlatformEvent<IViewPayload> platformEvent)
    {
        return TryGetCallbackInfo(platformEvent.Payload, out var callbackInfo)
               && handlerRegistry.Retrieve(callbackInfo) is { } handler
            ? handler
            : null;
    }

    /// <summary>
    /// Retrieves a <see cref="IHandler"/> from the incoming message.
    /// </summary>
    /// <param name="handlerRegistry">The <see cref="IHandlerRegistry"/> this method extends.</param>
    /// <param name="platformMessage">The incoming message.</param>
    /// <returns>The <see cref="IHandler"/> that should handle this message, or null if none is found or this message is not an interaction.</returns>
    public static IHandler? Retrieve(this IHandlerRegistry handlerRegistry, IPlatformMessage platformMessage)
    {
        return platformMessage.Payload.InteractionInfo is { CallbackInfo: InteractionCallbackInfo callbackInfo }
            && handlerRegistry.Retrieve(callbackInfo) is { } handler
                ? handler
                : null;
    }

    // Apply our precedence rules to retrieve the callback Id.
    // 1. The Action Id of the interaction element.
    // 2. The Block Id of the interaction element.
    // 3. The callback Id of the view.
    static bool TryGetCallbackInfo(IViewPayload viewPayload, [NotNullWhen(true)] out InteractionCallbackInfo? interactionCallbackInfo)
    {
        interactionCallbackInfo = null;
        return (viewPayload is IBlockActionsPayload blockActionsPayload
               && TryGetCallbackInfoFromActions(blockActionsPayload.Actions, out interactionCallbackInfo))
            || CallbackInfo.TryParseAs(viewPayload.View.CallbackId, out interactionCallbackInfo);
    }

    static bool TryGetCallbackInfoFromActions(
        IEnumerable<IPayloadElement> actions,
        [NotNullWhen(true)] out InteractionCallbackInfo? interactionCallbackInfo)
    {
        interactionCallbackInfo = actions.Select(a => CallbackInfo
                .TryGetCallbackInfoPayloadElement<InteractionCallbackInfo>(a, out var cb) ? cb : default)
            .FirstOrDefault(cb => cb is not null);

        return interactionCallbackInfo is not null;
    }
}
