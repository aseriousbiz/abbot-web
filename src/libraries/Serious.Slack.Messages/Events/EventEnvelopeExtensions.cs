namespace Serious.Slack.Events;

/// <summary>
/// Extensions to the <see cref="IEventEnvelope{EventBody}"/> class.
/// </summary>
public static class EventEnvelopeExtensions
{
    /// <summary>
    /// Attempts to return the channel Id that corresponds to the event.
    /// </summary>
    /// <param name="eventEnvelope">The event.</param>
    /// <returns></returns>
    public static string? GetChannelId(this IEventEnvelope<EventBody> eventEnvelope)
    {
        return eventEnvelope.Event switch
        {
            MessageEvent messageEvent => messageEvent.Channel,
            AppHomeOpenedEvent appHomeOpenedEvent => appHomeOpenedEvent.Channel,
            ChannelRenameEvent channelRenameEvent => channelRenameEvent.Channel.Id,
            _ => null
        };
    }
}
