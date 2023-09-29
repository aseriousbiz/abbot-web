using MassTransit;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Eventing;

public class EventingHealthCheck : IHealthCheck
{
    readonly IRequestClient<ConductHealthCheck> _client;

    public EventingHealthCheck(IRequestClient<ConductHealthCheck> client)
    {
        _client = client;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var resp = await _client.GetResponse<HealthCheckCompleted>(
                new() { ProducerMachine = Environment.MachineName },
                cancellationToken);

            if (resp.Message.IsHealthy)
            {
                return HealthCheckResult.Healthy($"Produced message in {Environment.MachineName}, consumed by {resp.Message.ConsumerMachine}");
            }

            return HealthCheckResult.Unhealthy(resp.Message.Error ?? "Unknown error returned by health check consumer");
        }
        catch (RequestFaultException rfex)
        {
            if (rfex.Fault?.Exceptions is [var ex, ..])
            {
                return HealthCheckResult.Unhealthy($"Request fault occurred: {ex.Message}", exception: rfex);
            }

            return HealthCheckResult.Unhealthy("Unknown request fault occurred", exception: rfex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
