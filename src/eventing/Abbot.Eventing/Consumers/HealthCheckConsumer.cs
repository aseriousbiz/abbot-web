using System.Reflection;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Eventing;

public class HealthCheckConsumer : IConsumer<ConductHealthCheck>
{
    readonly ILogger<HealthCheckConsumer> _logger;

    public HealthCheckConsumer(ILogger<HealthCheckConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ConductHealthCheck> context)
    {
        _logger.ConductingHealthCheck();
        await context.RespondAsync(new HealthCheckCompleted()
        {
            IsHealthy = true,
            HostAssembly = context.Host.Assembly,
            ConsumerMachine = Environment.MachineName,
        });
    }
}

static partial class HealthCheckConsumerLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Conducting health check.")]
    public static partial void ConductingHealthCheck(this ILogger<HealthCheckConsumer> logger);
}
