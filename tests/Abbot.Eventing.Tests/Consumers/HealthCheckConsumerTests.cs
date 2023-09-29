using Abbot.Common.TestHelpers;
using MassTransit;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Eventing.Consumers;

// Mostly just an example of how to write consumer tests :)
public class HealthCheckConsumerTests
{
    public class TheConsumeMethod
    {
        [Fact]
        public async Task RespondsHealthyAndWithHostAssembly()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<HealthCheckConsumer>()
                .Build();
            var client = env.Get<IRequestClient<ConductHealthCheck>>();

            var response = await client.GetResponse<HealthCheckCompleted>(new()
            {
                ProducerMachine = "Yar",
            });

            Assert.True(response.Message.IsHealthy);
            Assert.NotNull(response.Message.HostAssembly);
            Assert.NotEmpty(response.Message.HostAssembly);
        }
    }
}
