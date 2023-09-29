namespace Serious.Abbot.Eventing;

public class EventingOptions
{
    public const string InMemoryTransport = "InMemory";
    public const string AzureServiceBusTransport = "AzureServiceBus";

    public string Transport { get; set; } = InMemoryTransport;
    public string? Endpoint { get; set; }

    public bool Scheduler { get; set; } = true;
}
