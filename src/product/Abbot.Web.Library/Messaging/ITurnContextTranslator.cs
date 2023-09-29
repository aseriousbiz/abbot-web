using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Serious.Abbot.Events;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Takes an incoming message (turnContext) and translates it to an instance of
/// <see cref="IPlatformMessage" />.
/// </summary>
public interface ITurnContextTranslator
{
    /// <summary>
    /// Returns an <see cref="IPlatformMessage" /> appropriate to the incoming message.
    /// </summary>
    /// <param name="turnContext">The incoming chat message.</param>
    Task<IPlatformMessage?> TranslateMessageAsync(ITurnContext turnContext);

    /// <summary>
    /// Returns an <see cref="IPlatformMessage" /> when Abbot is installed.
    /// </summary>
    /// <param name="turnContext">The incoming install event.</param>
    Task<InstallEvent> TranslateInstallEventAsync(ITurnContext turnContext);

    /// <summary>
    /// Returns an <see cref="IPlatformMessage" /> when Abbot is uninstalled.
    /// </summary>
    /// <param name="turnContext">The incoming install event.</param>
    Task<IPlatformEvent?> TranslateUninstallEventAsync(ITurnContext turnContext);

    /// <summary>
    /// Returns an <see cref="IPlatformMessage" /> when Abbot receives a non-chat-message event.
    /// </summary>
    /// <param name="turnContext">The incoming event.</param>
    Task<IPlatformEvent?> TranslateEventAsync(ITurnContext turnContext);
}
