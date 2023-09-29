using System.Collections.Generic;
using System.Threading;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Messaging;

namespace Serious.Abbot.Infrastructure.Middleware;

/// <summary>
/// Bot Framework middleware that runs an <see cref="IMessageFormatter"/> on outgoing messages.
/// </summary>
public class MessageFormatMiddleware : IMiddleware
{
    readonly IMessageFormatter _formatter;

    /// <summary>
    /// Constructs a <see cref="MessageFormatMiddleware"/> injecting every <see cref="IMessageFormatter"/>
    /// in the Abbot.Web.Library assembly.
    /// </summary>
    /// <param name="formatter"></param>
    public MessageFormatMiddleware(IMessageFormatter formatter)
    {
        _formatter = formatter;
    }

    /// <summary>
    /// Handles the <see cref="ITurnContext{T}.OnSendActivities"/> event to format outgoing text.
    /// </summary>
    /// <param name="turnContext">The context object for this turn.</param>
    /// <param name="next">The delegate to call to continue the bot middleware pipeline.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    public async Task OnTurnAsync(
        ITurnContext turnContext,
        NextDelegate next,
        CancellationToken cancellationToken = new())
    {
        _formatter.NormalizeIncomingMessage(turnContext.Activity);

        async Task<ResourceResponse[]> SendActivitiesHandler(
            ITurnContext _,
            List<Activity> activities,
            Func<Task<ResourceResponse[]>> nextSend)
        {
            foreach (var activity in activities)
            {
                HandleOutgoingActivity(turnContext, activity);
            }

            // run full pipeline
            return await nextSend().ConfigureAwait(false);
        }

        async Task<ResourceResponse> UpdateActivityHandler(
            ITurnContext _,
            Activity activity,
            Func<Task<ResourceResponse>> nextSend)
        {
            HandleOutgoingActivity(turnContext, activity);

            // run full pipeline
            return await nextSend().ConfigureAwait(false);
        }

        turnContext.OnSendActivities(SendActivitiesHandler);
        turnContext.OnUpdateActivity(UpdateActivityHandler);

        // process bot logic
        await next(cancellationToken).ConfigureAwait(false);
    }

    void HandleOutgoingActivity(ITurnContext turnContext, Activity activity)
    {
        // Unwrap abbot-specific channel data, if any
        if (activity.ChannelData is AbbotChannelData abbotData)
        {
            // Restore the original channel data, we don't need to smuggle data using that value anymore.
            // If the formatter needs this data someday, we can always add it as a parameter to `IMessageFormatter`.
            activity.ChannelData = abbotData.InnerChannelData;

            // TurnContext always sets Conversation for us.
            // So we use Abbot metadata to smuggle across a new value when we want to force a message to a new conversation.
            if (abbotData.OverriddenMessageTarget is not null)
            {
                activity.Conversation = ToConversation(abbotData.OverriddenMessageTarget);
            }
        }

        _formatter.FormatOutgoingMessage(activity, turnContext);
    }

    static ConversationAccount ToConversation(IMessageTarget messageTarget)
    {
        var targetAddress = messageTarget.Address;
        var conversationId = new SlackConversationId(targetAddress.Id, targetAddress.ThreadId);
        return new ConversationAccount
        {
            Id = conversationId.ToString()
        };
    }
}
