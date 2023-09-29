using MassTransit;
using MassTransit.Introspection;

namespace Serious.Abbot.Eventing;

public class TopologyCatalog
{
    readonly IBus _bus;

    public TopologyCatalog(IBus bus)
    {
        _bus = bus;
    }

    public ProbeResult GetTopology()
    {
        var result = _bus.GetProbeResult();
        return result;
    }
}
