using Serious.Abbot.BotFramework;

namespace Microsoft.Bot.Schema;

public static class AbbotChannelDataExtensions
{
    /// <summary>
    /// If not already wrapped, wraps the activity's <see cref="IActivity.ChannelData"/> in an <see cref="AbbotChannelData"/>,
    /// then executes the provided function on the wrapped channel data to update it.
    /// </summary>
    /// <param name="activity">The activity to update Abbot-specific channel data for.</param>
    /// <param name="updater">A function to run on the <see cref="AbbotChannelData"/> to update it.</param>
    static void UpdateAbbotMetadata(this IActivity activity, Action<AbbotChannelData> updater)
    {
        if (activity.ChannelData is AbbotChannelData abbotData)
        {
            updater(abbotData);
        }
        else
        {
            abbotData = new AbbotChannelData(activity.ChannelData);
            activity.ChannelData = abbotData;
            updater(abbotData);
        }
    }

    /// <summary>
    /// Forces the activity to be sent to a specific conversation. Overrides the value in <see cref="IActivity.Conversation"/>
    /// </summary>
    /// <param name="activity">The outgoing activity to set the outgoing conversation for.</param>
    /// <param name="thread">A <see cref="ConversationAccount"/> referring to the conversation to send the message on.</param>
    public static void OverrideDestination(this IActivity activity, IMessageTarget thread)
    {
        activity.UpdateAbbotMetadata(a => a.OverriddenMessageTarget = thread);
    }

    public static IMessageTarget? GetOverriddenDestination(this IActivity activity)
    {
        return activity.ChannelData is AbbotChannelData abbotData
            ? abbotData.OverriddenMessageTarget
            : null;
    }
}
