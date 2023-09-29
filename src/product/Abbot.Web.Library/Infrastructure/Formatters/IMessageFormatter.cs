using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace Serious.Abbot.Infrastructure;

/// <summary>
/// Normalize incoming and format outgoing messages for a specific channel.
/// </summary>
public interface IMessageFormatter
{
    /// <summary>
    /// Given outgoing message text in Abbot format, formats the text for the channel.
    /// </summary>
    /// <param name="activity">The outgoing activity.</param>
    /// <param name="turnContext">The context object for this turn.</param>
    void FormatOutgoingMessage(Activity activity, ITurnContext turnContext);

    /// <summary>
    /// Normalizes an incoming message to the normalized Abbot format.
    /// </summary>
    /// <param name="activity">The incoming activity.</param>
    void NormalizeIncomingMessage(Activity activity);
}
