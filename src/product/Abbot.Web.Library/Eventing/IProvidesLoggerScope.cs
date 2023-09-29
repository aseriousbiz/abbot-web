using MassTransit;
using Microsoft.Extensions.Logging;

namespace Serious.Abbot.Eventing;

/// <summary>
/// An interface MassTransit messages can implement to provide a logger scope that will be begun any time this message is consumed.
/// </summary>
// This isn't for polymorphic publishing, it's for logging purposes.
// So exclude it from the topology.
// Without this, we'd end up with an IProvidesLoggerScope topic in Azure Service Bus!
[ExcludeFromTopology]
public interface IProvidesLoggerScope
{
    // For now we hand-implement this.
    // But we could have a source generator come along and scan message properties for an attribute that indicates they should be in the logger scope.
    //
    // If the scope properties get too big, there's an alternate strategy:
    // We can log the "scope" properties _once_ at consume time.
    // That way, we don't have to log them every time we log a message.
    // However, when diagnosing an issue, you'll need to pull all the events for a given message ID and find the "scope" event.
    IDisposable? BeginScope(ILogger logger);
}
