using System.Threading.Tasks;
using Serious.Abbot.Messaging;
using Serious.Abbot.Skills;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Used to dismiss a message. Just route the button to this handler. This way each handler
/// doesn't have to implement dismissal.
/// </summary>
public class DismissHandler : IHandler
{
    /// <summary>
    /// Handles a message interaction by dismissing the message.
    /// </summary>
    /// <param name="platformMessage"></param>
    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        if (platformMessage.Payload.InteractionInfo is { ResponseUrl: { } responseUrl })
        {
            await platformMessage.Responder.DeleteActivityAsync(responseUrl);
        }
    }
}
