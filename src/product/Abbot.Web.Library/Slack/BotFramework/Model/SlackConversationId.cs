namespace Serious.Abbot.Messaging;

/// <summary>
/// Represents information about the channel and thread in a conversation Id. This still makes use of the old
/// format.
/// as well as the old Bot Framework format <c>{BotId}:{TeamId}:{ChannelId}</c> or
/// <c>{BotId}:{TeamId}:{ChannelId}:{ThreadTimestamp}</c>.
/// </summary>
/// <param name="ChannelId">The Id of the Slack channel.</param>
/// <param name="ThreadTimestamp">The thread timestamp used to respond to a thread.</param>
public readonly record struct SlackConversationId(string ChannelId, string? ThreadTimestamp)
{
    /// <summary>
    /// Parses the Bot Framework conversation Id format <c>{BotId}:{TeamId}:{ChannelId}</c> or
    /// <c>{BotId}:{TeamId}:{ChannelId}:{ThreadTimestamp}</c>, but ignores BotId and TeamId as we don't need
    /// those to be part of the conversation Id any more.
    /// </summary>
    /// <param name="value">The conversation Id value.</param>
    /// <param name="conversationId">The resulting <see cref="SlackConversationId"/>.</param>
    /// <returns></returns>
    public static bool TryParse(string value, out SlackConversationId conversationId)
    {
        var splat = value.Split(':');
        switch (splat.Length)
        {
            case 1 when splat[0].Length > 0:
                conversationId = new SlackConversationId(splat[0], null);
                return true;
            case < 3:
                conversationId = default;
                return false;
            default:
                // We ignore the first two parts of the old conversation id.
                conversationId = new SlackConversationId(splat[2], splat.Length > 3 ? splat[3] : null);
                return true;
        }
    }

    /// <summary>
    /// Renders this Id as a string in the format <c>::{ChannelId}</c> or <c>::{ChannelId}:{ThreadTimestamp}</c>.
    /// </summary>
    /// <remarks>
    /// The weird format is for back-compat reasons. The old Bot Framework Id Format was
    /// <c>{BotId}:{TeamId}:{ChannelId}</c> or <c>{BotId}:{TeamId}:{ChannelId}:{ThreadTimestamp}</c>. But we no
    /// longer need the BotId and TeamId to be part of the conversation Id.
    /// </remarks>
    /// <returns>A formatted string representation of the Id.</returns>
    public override string ToString()
    {
        // We no longer need to include the BotId and the TeamId, but for back-compat reasons, we'll
        // continue to support the old format for now, hence the weird `::`.
        var baseName = $"::{ChannelId}";
        return ThreadTimestamp is { Length: > 0 } ? $"{baseName}:{ThreadTimestamp}" : baseName;
    }
}
