namespace Serious.Abbot.Eventing.Messages
{
    public record ConductHealthCheck
    {
        public required string ProducerMachine { get; init; }
    }

    public record HealthCheckCompleted
    {
        public required string ConsumerMachine { get; init; }
        public required bool IsHealthy { get; init; }
        public string? HostAssembly { get; init; }
        public string? Error { get; init; }
    }
}
