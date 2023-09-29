using System.Collections.Generic;
using System.Linq;
using MassTransit;
using MassTransit.Configuration;
using MassTransit.SagaStateMachine;
using MassTransit.Visualizer;
using Serious.Abbot.Eventing.StateMachines;

namespace Serious.Abbot.Pages.Staff.Bus;

public class IndexModel : StaffToolsPage
{
    readonly IServiceProvider _services;
    readonly IEndpointNameFormatter _endpointNameFormatter;
    readonly List<IConsumerDefinition> _consumers;
    readonly List<ISagaDefinition> _sagas;

    public List<SagaModel> Sagas { get; set; } = null!;

    public List<ConsumerModel> Consumers { get; set; } = null!;

    public IndexModel(IEnumerable<IConsumerRegistration> consumerRegistrations, IEnumerable<ISagaRegistration> sagaRegistrations, IServiceProvider services, IEndpointNameFormatter endpointNameFormatter)
    {
        _consumers = consumerRegistrations.Select(c => c.GetDefinition(services)).ToList();
        _sagas = sagaRegistrations.Select(s => s.GetDefinition(services)).ToList();
        _services = services;
        _endpointNameFormatter = endpointNameFormatter;
    }

    public void OnGet()
    {
        Consumers = _consumers.Select(BuildConsumerModel).ToList();
        Sagas = _sagas.Select(BuildSagaModel).ToList();
    }

    ConsumerModel BuildConsumerModel(IConsumerDefinition consumerDefinition) =>
        new(consumerDefinition.GetEndpointName(_endpointNameFormatter),
            consumerDefinition.ConsumerType.FullName.Require());

    SagaModel BuildSagaModel(ISagaDefinition sagaDefinition)
    {
        // Try to get the state machine
        string? graphviz = null;
        string? stateMachineType = null;
        if (_services.GetService(typeof(SagaStateMachine<>).MakeGenericType(sagaDefinition.SagaType)) is StateMachine
            stateMachine)
        {
            stateMachineType = stateMachine.GetType().FullName;

            // HACKY AF but it works.
            dynamic dynStateMachine = stateMachine;
            var graph = (StateMachineGraph)GraphStateMachineExtensions.GetGraph(dynStateMachine);
            var visualizer = new StateMachineGraphvizGenerator(graph);
            graphviz = visualizer.CreateDotFile();
        }

        return new(sagaDefinition.GetEndpointName(_endpointNameFormatter),
            sagaDefinition.SagaType.FullName.Require(),
            stateMachineType,
            graphviz);
    }


    public record SagaModel(string EndpointName, string TypeName, string? StateMachineType, string? StateMachineGraph);

    public record ConsumerModel(string EndpointName, string TypeName);
}
