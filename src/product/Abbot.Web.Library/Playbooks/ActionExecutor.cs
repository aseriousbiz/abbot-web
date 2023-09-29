using System.Collections.Generic;
using MassTransit;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Executes an action.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Executes the action given the provided <see cref="StepContext"/>
    /// </summary>
    /// <param name="context">A <see cref="StepContext"/> that describes the context in which the action is executing.</param>
    /// <returns>A <see cref="StepResult"/> that describes the result of executing the step.</returns>
    Task<StepResult> ExecuteStepAsync(StepContext context);
}

/// <summary>
/// An <see cref="IActionExecutor"/> that can be suspended, resumed, and cleaned up.
/// </summary>
public interface ISuspendableExecutor
{
    /// <summary>
    /// Cleans up resources from a suspended step.
    /// </summary>
    /// <param name="context">The <see cref="StepContext"/> for the cancelled step</param>
    Task DisposeSuspendedStepAsync(StepContext context);
}
