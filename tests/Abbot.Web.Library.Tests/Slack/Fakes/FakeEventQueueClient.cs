using System.Threading.Tasks;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;

namespace Serious.Slack.Tests.Fakes;

public class FakeEventQueueClient : IEventQueueClient
{
    public Task EnqueueEventAsync(IEventEnvelope<EventBody> envelope, string eventBody, int? integrationId, int retryNumber)
    {
        Enqueued = new EnqueuedEvent(envelope, eventBody);
        return Task.CompletedTask;
    }

    public EnqueuedEvent Enqueued { get; private set; }

    public record struct EnqueuedEvent(IEventEnvelope<EventBody> EventEnvelope, string EventBody);
}
