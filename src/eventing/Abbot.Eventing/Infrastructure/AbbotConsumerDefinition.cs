using System.Diagnostics;
using MassTransit;

namespace Serious.Abbot.Eventing.Infrastructure;

public abstract class AbbotConsumerDefinition<TConsumer> : ConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
{
    bool _requireSession;
    bool _configured;

    protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator, IConsumerConfigurator<TConsumer> consumerConfigurator)
    {
        _configured = true;
        if (_requireSession && endpointConfigurator is IServiceBusReceiveEndpointConfigurator sbCfg)
        {
            sbCfg.RequiresSession = true;
        }

        if (_requireSession && endpointConfigurator is IInMemoryReceiveEndpointConfigurator imCfg)
        {
            // Don't allow concurrency for session-based consumers in-memory
            imCfg.UseConcurrencyLimit(1);
        }
        base.ConfigureConsumer(endpointConfigurator, consumerConfigurator);
    }

    /// <summary>
    /// Configures the queue attached for this consumer to require sessions.
    /// The "RequiresSession" flag _cannot_ be changed once a queue is created in Azure Service Bus.
    /// So, we require specifying the endpoint name here to reinforce that.
    /// </summary>
    protected void RequireSession(string endpointName)
    {
        if (_configured)
        {
            throw new UnreachableException("You must call RequireSession in the constructor!");
        }
        _requireSession = true;
        EndpointName = endpointName;
    }
}
