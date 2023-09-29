using System.Runtime.CompilerServices;
using Serious.Abbot.Messaging;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;

[assembly: InternalsVisibleTo("Abbot.Web.Library.Tests")]
namespace Serious.Slack.BotFramework;

/// <summary>
/// Methods to translate Slack primitives to Bot Framework primitives and vice versa.
/// </summary>
static class SlackTranslator
{
    /// <summary>
    /// Create a <see cref="SlackConversationId" /> from an <see cref="Payload"/>
    /// </summary>
    /// <param name="payload">The interaction payload.</param>
    /// <returns>A Bot Framework formatted conversation Id.</returns>
    public static string? GetConversationIdFromInteractionPayload(IPayload payload)
    {
        var (channelId, threadTimestamp) = payload switch
        {
            IMessagePayload<SlackMessage> messagePayload => (
                messagePayload.Channel.Id,
                messagePayload.Message?.ThreadTimestamp),
            _ => (null, null)
        };
        return channelId is not null
            ? new SlackConversationId(channelId, threadTimestamp).ToString()
            : null;
    }
}
