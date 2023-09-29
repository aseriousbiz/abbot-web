using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;

namespace Serious.Slack.BotFramework;

public static class TurnContextExtensions
{
    const string SlackEventInfoKey = nameof(SlackEventInfoKey);
    const string IntegrationIdKey = "IntegrationId";

    /// <summary>
    /// Sends a rich formatted message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task<ResourceResponse> SendActivityAsync(this ITurnContext turnContext, string fallbackText, params ILayoutBlock[] blocks)
    {
        var richActivity = new RichActivity(fallbackText, blocks);
        return await turnContext.SendActivityAsync(richActivity);
    }

    /// <summary>
    /// Store the Slack Event Id in the TurnContext.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="eventInfo">The incoming event info.</param>
    public static void SetSlackEventId(this ITurnContext turnContext, SlackEventInfo eventInfo)
    {
        turnContext.TurnState[SlackEventInfoKey] = eventInfo;
    }

    /// <summary>
    /// Retrieve the slack event id from the turn context.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="eventInfo">The event info retrieved from the turn context, if any.</param>
    /// <returns><c>true</c> if it exists in the turn state, otherwise <c>false</c>.</returns>
    public static bool TryGetSlackEventId(this ITurnContext turnContext, out SlackEventInfo eventInfo)
    {
        if (turnContext.TurnState.TryGetValue(SlackEventInfoKey, out var slackEventInfo))
        {
            eventInfo = (SlackEventInfo)slackEventInfo;
            return true;
        }

        eventInfo = default;
        return false;
    }

    /// <summary>
    /// Store the Integration Id in the TurnContext.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <param name="integrationId">The integration Id of the incoming request retrieved from the query string.</param>
    public static void SetIntegrationId(this ITurnContext turnContext, int integrationId)
    {
        turnContext.TurnState[IntegrationIdKey] = integrationId;
    }

    /// <summary>
    /// Retrieve the Integration Id from the turn context.
    /// </summary>
    /// <param name="turnContext">A TurnContext representing the current incoming message and environment.</param>
    /// <returns>Integration Id or <c>null</c>.</returns>
    public static int? GetIntegrationId(this ITurnContext turnContext)
    {
        if (turnContext.TurnState.TryGetValue(IntegrationIdKey, out var integrationIdObject))
        {
            return (int)integrationIdObject;
        }

        return null;
    }
}
