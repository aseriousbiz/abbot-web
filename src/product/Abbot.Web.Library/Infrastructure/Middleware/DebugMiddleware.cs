using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.BotFramework;
using Serious.Abbot.Messaging;
using Serious.Slack.Events;

namespace Serious.Abbot.Infrastructure.Middleware;

public class DebugMiddleware : IMiddleware
{
    const string DebugMiddlewareFlag = nameof(DebugMiddlewareFlag);
    static readonly IReadOnlyCollection<string> DebugSuffixes = new[] { " --debug", " —debug" }.ToReadOnlyList();
    string? _incomingActivity;

    public async Task OnTurnAsync(
        ITurnContext turnContext,
        NextDelegate next,
        CancellationToken cancellationToken = new())
    {
        if (ContainsDebugFlag(turnContext))
        {
            _incomingActivity = GetIncomingDebugInformation(turnContext.Activity, "(Received)");
            turnContext.TurnState[DebugMiddlewareFlag] = "true";

            // Strip off the debug suffix so skills don't see it.
            // At this point, the MessageFormatMiddleware should have already run and normalized
            // the incoming text.
            string message = turnContext.Activity.Text ?? string.Empty;
            var cleanMessage = DebugSuffixes
                .Aggregate(
                    message,
                    (current, suffix) => current.TrimSuffix(suffix, StringComparison.Ordinal));
            turnContext.Activity.Text = cleanMessage;
        }

        turnContext.OnSendActivities(async (_, activities, nextSend) => {
            if (ContainsDebugFlag(turnContext))
            {
                foreach (var activity in activities)
                {
                    AppendDebugToActivityMessage(activity);
                    // An ugly HAAAACK for now to ensure we can see incoming debug info as long as the bot
                    // responds in some fashion.
                    if (_incomingActivity is not null)
                    {
                        activity.PrependToMessage(_incomingActivity);
                        _incomingActivity = null;
                    }
                }
            }

            return await nextSend().ConfigureAwait(false);
        });

        // process bot logic
        await next(cancellationToken).ConfigureAwait(false);
    }

    static bool ContainsDebugFlag(ITurnContext turnContext)
    {
        return turnContext.TurnState.ContainsKey(DebugMiddlewareFlag)
               || turnContext.IsMessage() && DebugSuffixes.Any(suffix => ContainsDebugSuffix(turnContext.Activity, turnContext.Activity.Text?.Trim(), suffix));
    }

    static bool ContainsDebugSuffix(IMessageActivity activity, string? activityText, string suffix)
    {
        if (activityText is null)
        {
            return false;
        }
        // When we are here, the incoming message should have been normalized by the
        // MessageFormatMiddleware and activity.Text should contain the message.
        // On outgoing messages, this should run before we take activity.Text and populate
        // channel data (in cases where that's necessary such as Slack).
        return activityText.EndsWith(suffix, StringComparison.Ordinal)
               || activity.Attachments?.Any(file => file.Name?.EndsWith(suffix, StringComparison.Ordinal) == true) == true;
    }

    static void AppendDebugToActivityMessage(Activity activity)
    {
        var debugInfo = GetOutgoingDebugInformation(activity, "(Sent)");

        activity.AppendToMessage(debugInfo);
    }

    static IEventEnvelope<EventBody>? GetIncomingEventData(object channelData)
    {
        return channelData switch
        {
            IEventEnvelope<EventBody> eventEnvelope => eventEnvelope,
            JObject jObject => jObject.ToObject<SlackChannelData>()?.SlackMessage as IEventEnvelope<EventBody>,
            _ => null
        };
    }

    static string GetSafeSlackChannelData(object channelData)
    {
        var messageEvent = GetIncomingEventData(channelData);
        if (messageEvent is null)
        {
            return "_{Incoming event is null or not what we expect}_";
        }

        var message = messageEvent.Event as MessageEvent;

        var details = $@"
    team_id:    {messageEvent.TeamId}
    api_app_id: {messageEvent.ApiAppId}
    event_id:   {messageEvent.EventId}
    event_time: {messageEvent.EventTime}
    is_ext_shared_channel: {messageEvent.IsExternallySharedChannel}";
        if (message is not null)
        {
            details += $@"
    event:
        client_msg_id: {message.ClientMessageId}
        ts:      {message.Timestamp}
        thread_ts:{message.ThreadTimestamp}
        type:   {message.Type}
        channel:{message.Channel}
        user:   {message.User}
        team:   {message.Team}
        source_team: {message.SourceTeam}
    authorizations: {JsonConvert.SerializeObject(messageEvent.Authorizations)}
";
        }
        return details;
    }

    static string GetIncomingDebugInformation(IMessageActivity activity, string messageStatus)
    {
        var channelId = activity.ChannelId ?? string.Empty;

        bool isSlack = channelId.Equals("slack", StringComparison.Ordinal);

        var channelData = isSlack
            ? GetSafeSlackChannelData(activity.ChannelData)
            : string.Empty;

        var slackEscape = SlackEscape(isSlack);
        return $@"

```
Abbot Debug Information {messageStatus}
---------------------------------
Id: {activity.Id}
Type: {activity.Type}
Text: {slackEscape(activity.Text)}
TextFormat: {activity.TextFormat}
Conversation: {activity.Conversation?.Id}
ReplyToId: {activity.ReplyToId}
ChannelId: {activity.ChannelId}
EventEnvelope: {slackEscape(channelData)}
```
    ";
    }

    static string GetOutgoingDebugInformation(IMessageActivity activity, string messageStatus)
    {
        var channelId = activity.ChannelId ?? string.Empty;

        bool isSlack = channelId.Equals("slack", StringComparison.Ordinal);
        var slackEscape = SlackEscape(isSlack);
        return $@"

```
Abbot Debug Information {messageStatus}
---------------------------------
Id: {activity.Id}
Type: {activity.Type}
Text: {slackEscape(activity.Text)}
TextFormat: {activity.TextFormat}
Conversation: {activity.Conversation?.Id}
Recipient: {activity.Recipient?.Id}
ReplyToId: {activity.ReplyToId}
Timestamp: {activity.Timestamp}
Local Timestamp: {activity.LocalTimestamp}
ChannelId: {activity.ChannelId}
ChannelData: {slackEscape(JsonConvert.SerializeObject(activity.ChannelData, Formatting.Indented))}
```
    ";
    }

    static Func<string?, string> SlackEscape(bool isSlack)
    {
        return isSlack
            ? s => s is null
                ? "_(null)_"
                : s.Replace("<", @"&lt;", StringComparison.Ordinal)
                    .Replace(">", @"&gt;", StringComparison.Ordinal)
            : new Func<string?, string>(s => s ?? "_(null)_");

    }
}
