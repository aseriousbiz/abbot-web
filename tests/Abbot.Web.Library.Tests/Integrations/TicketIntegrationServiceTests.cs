using System.Collections.Generic;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;
using Xunit;

public class TicketIntegrationServiceTests
{
    public class TheEnqueueTicketLinkRequestMethod
    {
        [Fact]
        public async Task EnqueuesTicketLinkRequest()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            var ticketService = env.Activate<TicketIntegrationService>();

            var properties = new Dictionary<string, object?>
            {
                ["subject"] = "Subject",
                ["content"] = "Description",
                ["hs_ticket_priority"] = "MEDIUM",
                ["tags"] = new[] { "tag1", "tag2" },
            };
            ticketService.EnqueueTicketLinkRequest<ITicketingSettings>(
                new Id<Integration>(246),
                convo,
                env.TestData.Member,
                properties);

            env.BackgroundJobClient.DidEnqueue<TicketLinkerJob<ITicketingSettings>>(
                x => x.LinkConversationToTicketAsync(
                    env.TestData.Organization,
                    new Id<Integration>(246),
                    convo,
                    convo.GetFirstMessageUrl(),
                    env.TestData.Member,
                    env.TestData.Member.Organization,
                    properties));
        }
    }
}
