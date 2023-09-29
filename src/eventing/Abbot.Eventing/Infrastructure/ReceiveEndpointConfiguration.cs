using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.Services;

namespace Serious.Abbot.Eventing.Infrastructure;

public class ReceiveEndpointConfiguration : IConfigureReceiveEndpoint, IConsumerConfigurationObserver, ISagaConfigurationObserver
{
    readonly IServiceProvider _services;
    readonly DiagnosticConsumeFilter _consumeFilter;

    public ReceiveEndpointConfiguration(IServiceProvider services, DiagnosticConsumeFilter consumeFilter)
    {
        _services = services;
        _consumeFilter = consumeFilter;
    }

    public void Configure(string name, IReceiveEndpointConfigurator configurator)
    {
        configurator.UseFilter(_consumeFilter);
        configurator.ConnectSagaConfigurationObserver(this);
        configurator.ConnectConsumerConfigurationObserver(this);
    }

    public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator) where TConsumer : class
    {
    }

    public void ConsumerMessageConfigured<TConsumer, TMessage>(IConsumerMessageConfigurator<TConsumer, TMessage> configurator) where TConsumer : class where TMessage : class
    {
        var filter = _services.Activate<DiagnosticConsumerConsumeFilter<TConsumer, TMessage>>();
        configurator.UseFilter(filter);
    }

    public void SagaConfigured<TSaga>(ISagaConfigurator<TSaga> configurator) where TSaga : class, ISaga
    {
    }

    public void StateMachineSagaConfigured<TInstance>(ISagaConfigurator<TInstance> configurator, SagaStateMachine<TInstance> stateMachine) where TInstance : class, ISaga, SagaStateMachineInstance
    {
        var observer = _services.Activate<DiagnosticStateMachineObserver<TInstance>>();
        stateMachine.ConnectEventObserver(observer);
        stateMachine.ConnectStateObserver(observer);
    }

    public void SagaMessageConfigured<TSaga, TMessage>(ISagaMessageConfigurator<TSaga, TMessage> configurator) where TSaga : class, ISaga where TMessage : class
    {
    }
}
