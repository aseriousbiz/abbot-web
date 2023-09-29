using System.Threading.Tasks;
using Serious.Slack.Events;

namespace Serious.Slack.BotFramework;

/// <summary>
/// Type that can enqueue a Slack event to be processed.
/// </summary>
public interface IEventQueueClient
{
    /// <summary>
    /// Processes the Slack event.
    /// </summary>
    /// <param name="envelope">The Slack event envelope.</param>
    /// <param name="eventBody">The event body as a string.</param>
    /// <param name="integrationId">Identifies a custom instance of Abbot, if present.</param>
    /// <param name="retryNumber">The retry number of the event.</param>
    Task EnqueueEventAsync(IEventEnvelope<EventBody> envelope, string eventBody, int? integrationId, int retryNumber);
}
