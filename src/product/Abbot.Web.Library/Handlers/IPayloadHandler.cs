using System.Threading.Tasks;
using Serious.Abbot.Events;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles Slack events other than messages and view interactions.
/// </summary>
/// <remarks>
/// Payload handlers in this assembly are registered by default. See <see cref="PayloadHandlerRegistry"/> for
/// how they are retrieved and invoked.
/// </remarks>
public interface IPayloadHandler<in TPayload>
{
    /// <summary>
    /// Handles the incoming platform event.
    /// </summary>
    /// <param name="platformEvent">The incoming platform event with a view block actions payload.</param>
    /// <typeparam name="TPayload">The platform event payload type.</typeparam>
    Task OnPlatformEventAsync(IPlatformEvent<TPayload> platformEvent);
}
