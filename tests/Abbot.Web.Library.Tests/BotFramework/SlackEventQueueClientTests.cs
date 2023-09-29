using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.BotFramework;
using Serious.Slack.Events;

public class SlackEventQueueClientTests
{
    public class TheEnqueueEventAsyncMethod
    {
        [Fact]
        public async Task EnqueuesEventProcessingJob()
        {
            var env = TestEnvironment.Create();
            var eventEnvelope = new EventEnvelope<MessageEvent>
            {
                TeamId = "T08675309",
                Event = new MessageEvent
                {
                    User = "U012345678",
                    Timestamp = "1234567890",
                    Team = "T08675309",
                    Channel = "C00000001",
                },
                EventId = "EV000000001",
                ApiAppId = "A00000001",
            };
            var client = env.Activate<SlackEventQueueClient>();

            await client.EnqueueEventAsync(eventEnvelope, "{\"type\":\"whatevs\"}", null, 0);

            var slackEvent = await env.Db.SlackEvents.SingleAsync();

            Assert.Equal("EV000000001", slackEvent.EventId);
            Assert.Equal("{\"type\":\"whatevs\"}", slackEvent.Content.Reveal());
            var (enqueuedJob, state) = Assert.Single(env.BackgroundJobClient.EnqueuedJobs);
            Assert.Equal("Enqueued", state.Name);
            Assert.Equal(typeof(SlackEventProcessor), enqueuedJob.Type);
            Assert.Equal(nameof(SlackEventProcessor.ProcessEventAsync), enqueuedJob.Method.Name);
            Assert.Equal(slackEvent.Id, enqueuedJob.Args[0]);
        }
    }
}
