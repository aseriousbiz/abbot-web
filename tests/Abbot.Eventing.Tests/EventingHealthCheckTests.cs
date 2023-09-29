using Abbot.Common.TestHelpers;
using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Eventing;

public class EventingHealthCheckTests
{
    public class TheCheckHealthAsyncMethod
    {
        [Fact]
        public async Task ReportsHealthyIfBusRequestCompletesWithIsHealthyTrue()
        {
            var env = TestEnvironmentBuilder.Create()
                .ConfigureBus(cfg => {
                    cfg.AddHandler(async (ConductHealthCheck c) => new HealthCheckCompleted()
                    {
                        IsHealthy = true,
                        HostAssembly = "TheAssembly",
                        ConsumerMachine = "Blarg",
                    });
                })
                .Build();

            var healthCheck = env.Activate<EventingHealthCheck>();

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.StartsWith($"Produced message in {Environment.MachineName}, consumed by Blarg", result.Description);
        }

        [Theory]
        [InlineData(null, "Unknown error returned by health check consumer")]
        [InlineData("Boom", "Boom")]
        public async Task ReportsUnhealthyIfBusRequestCompletesWithIfHealthyFalse(string? consumerError, string resultDescription)
        {
            var env = TestEnvironmentBuilder.Create()
                .ConfigureBus(cfg => {
                    cfg.AddHandler(async (ConsumeContext<ConductHealthCheck> c) => new HealthCheckCompleted()
                    {
                        IsHealthy = false,
                        Error = consumerError,
                        ConsumerMachine = "Blarg",
                    });
                })
                .Build();

            var healthCheck = env.Activate<EventingHealthCheck>();

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Equal(resultDescription, result.Description);
        }

        [Fact]
        public async Task ReportsUnhealthyIfBusFails()
        {
            var env = TestEnvironmentBuilder.Create()
                .ConfigureBus(cfg => {
                    cfg.AddHandler(async (ConsumeContext<ConductHealthCheck> c) => throw new Exception("Yar"));
                })
                .Build();

            var healthCheck = env.Activate<EventingHealthCheck>();

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

            Assert.Equal(HealthStatus.Unhealthy, result.Status);
            Assert.Equal("Request fault occurred: Yar", result.Description);
        }
    }
}
