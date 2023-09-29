using System.Collections.Generic;
using System.Linq;
using MassTransit;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Playbooks;

public class ActionDispatcher
{
    readonly Dictionary<string, IActionType> _actionTypes;

    public ActionDispatcher(IEnumerable<IActionType> actionTypes)
    {
        _actionTypes = actionTypes.ToDictionary(t => t.Type.Name);
    }

    /// <summary>
    /// Executes the provided step, using services from the provided <see cref="IServiceProvider"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceProvider"/> containing scoped services appropriate for activating the <see cref="IActionExecutor"/> that will execute this step.</param>
    /// <param name="context">A <see cref="StepContext"/> that describes the context in which the action is executing.</param>
    /// <returns>A <see cref="StepResult"/> that describes the result of executing the step.</returns>
    public async Task<StepResult> ExecuteStepAsync(IServiceProvider services, StepContext context)
    {
        if (!_actionTypes.TryGetValue(context.Step.Type, out var executorType))
        {
            throw new InvalidOperationException($"Executor for action '{context.Step.Type}' not found.");
        }

        var actionExecutor = executorType.CreateExecutor(services);
        return await actionExecutor.ExecuteStepAsync(context);
    }

    /// <summary>
    /// Cleans up resources from a suspended step, using services from the provided <see cref="IServiceProvider"/>.
    /// </summary>
    public async Task DisposeSuspendedStepAsync(IServiceProvider services, StepContext context)
    {
        if (!_actionTypes.TryGetValue(context.Step.Type, out var executorType))
        {
            return;
        }

        var actionExecutor = executorType.CreateExecutor(services);
        if (actionExecutor is ISuspendableExecutor suspendable)
        {
            await suspendable.DisposeSuspendedStepAsync(context);
        }
    }
}
