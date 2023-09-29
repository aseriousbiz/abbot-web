using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Serialization;
using Serious.Slack.BotFramework.Model;

namespace Serious.Abbot.BotFramework;

public static class ActivityExtensions
{
    internal static T GetChannelData<T>(this ITurnContext turnContext)
    {
        var activity = turnContext.Activity;
        if (activity.ChannelData is not JObject channelDataJson)
        {
            throw new InvalidOperationException($"{typeof(T).Name} is not a JObject as expected.");
        }

        var channelData = AbbotJsonFormat.NewtonsoftJson.JsonToObject<T>(channelDataJson);

        return channelData
               ?? throw new InvalidOperationException(
                   $"ChannelData could not be cast to {typeof(T).Name}.");
    }

    /// <summary>
    /// Append text to the outgoing activity.
    /// </summary>
    /// <param name="activity">The activity representing the message to send to the chat platform.</param>
    /// <param name="value">The value to append.</param>
    public static void AppendToMessage(this Activity activity, string value)
    {
        if (activity.ChannelData is MessageChannelData messageChannelData)
        {
            var newText = messageChannelData.Message.Text + value;
            var message = messageChannelData.Message with { Text = newText };
            activity.ChannelData = messageChannelData with
            {
                Message = message
            };
        }
        else
        {
            activity.Text += value;
        }
    }

    /// <summary>
    /// Prepend text to the outgoing activity.
    /// </summary>
    /// <param name="activity">The activity representing the message to send to the chat platform.</param>
    /// <param name="value">The value to prepend.</param>
    public static void PrependToMessage(this Activity activity, string value)
    {
        if (activity.ChannelData is MessageChannelData messageChannelData)
        {
            var newText = value + messageChannelData.Message.Text;
            var message = messageChannelData.Message with { Text = newText };
            activity.ChannelData = messageChannelData with
            {
                Message = message
            };
        }
        else
        {
            activity.Text += value + activity.Text;
        }
    }

    /// <summary>
    /// Returns true if the incoming turn context is a message. It's used by the DebugMiddleware.
    /// </summary>
    /// <param name="turnContext">The incoming chat message.</param>
    public static bool IsMessage(this ITurnContext turnContext)
    {
        return turnContext is ITurnContext<IMessageActivity>
               || turnContext.Activity.Type is "app_mention" or "message";
    }

    /// <summary>
    /// Retrieves the message text from the outgoing message.
    /// </summary>
    /// <param name="activity">The outgoing message.</param>
    public static string GetReplyMessageText(this IMessageActivity activity)
    {
        return activity.ChannelData is MessageChannelData messageChannelData
            ? messageChannelData.Message.Text?.Trim() ?? string.Empty
            : activity.Text?.Trim() ?? string.Empty;
    }
}
