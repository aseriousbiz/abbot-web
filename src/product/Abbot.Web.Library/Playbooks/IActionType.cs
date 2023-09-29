using Serious.Abbot.Services;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Describes the type of an action <see cref="Step"/>.
/// </summary>
public interface IActionType
{
    /// <summary>
    /// Gets the <see cref="StepType"/> that describes this action.
    /// </summary>
    StepType Type { get; }

    /// <summary>
    /// Creates an <see cref="IActionExecutor"/> to execute this action.
    /// </summary>
    /// <param name="services">A <see cref="IServiceProvider"/> that contains services that are appropriately-scoped for a single invocation of a step.</param>
    /// <returns>The <see cref="IActionExecutor"/> to use to run the action.</returns>
    IActionExecutor CreateExecutor(IServiceProvider services);
}

/// <summary>
/// Base class for an <see cref="IActionType"/> that uses a specific <see cref="IActionExecutor"/>.
/// </summary>
/// <typeparam name="TExecutor">The type of the <see cref="IActionExecutor"/></typeparam>
public abstract class ActionType<TExecutor> : IActionType
    where TExecutor : IActionExecutor
{
    public abstract StepType Type { get; }

    public IActionExecutor CreateExecutor(IServiceProvider services) =>
        // This activates a new executor for each invocation.
        // This is the safest thing to do from a service lifetime perspective.
        // It allows executors to depend on scoped and transient services "for free" since they will be activated once for each invocation.
        // As an optimization later, we could consider having some Action Types return singleton executors,
        // as long as they either don't depend on scoped/transient services or they use `IServiceScopeProvider` to resolve them.
        services.Activate<TExecutor>();
}
