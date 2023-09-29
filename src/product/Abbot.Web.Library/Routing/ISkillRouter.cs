using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Services;

namespace Serious.Abbot.Infrastructure;

/// <summary>
/// Routes incoming messages to the appropriate skill that will handle the message.
/// </summary>
public interface ISkillRouter
{
    /// <summary>
    /// Retrieves a <see cref="RouteResult"/> for the incoming message. This result includes the resolved
    /// skill if any. Some commands to Abbot won't match a skill, but should be passed to the
    /// <see cref="ISkillNotFoundHandler"/>. In other cases the message should be ignored, in which case
    /// this returns a <see cref="RouteResult"/>.
    /// </summary>
    /// <param name="platformMessage">The incoming chat message.</param>
    /// <returns>A <see cref="RouteResult"/> with the result of routing.</returns>
    Task<RouteResult> RetrieveSkillAsync(IPlatformMessage platformMessage);

    /// <summary>
    /// Routes the <see cref="IPlatformEvent"/> to the corresponding <see cref="IPayloadHandler{TPayload}"/> and returns
    /// a delegate that calls the system skill with the event.
    /// </summary>
    /// <param name="platformEvent">The incoming platform event.</param>
    /// <returns>A func that calls the system skill with the correctly typed platform event.</returns>
    PayloadHandlerRouteResult RetrievePayloadHandler(IPlatformEvent platformEvent);
}
