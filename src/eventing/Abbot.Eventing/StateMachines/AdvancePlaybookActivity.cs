using System.Diagnostics;
using MassTransit;
using MassTransit.Logging;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Eventing.StateMachines;

/// <summary>
/// State machine activity that advances the playbook run state to the next step.
/// </summary>
/// <remarks>
/// This activity ASSUMES that ONE of the following is true:
/// * No steps have been executed for this Playbook (<see cref="PlaybookRunProperties.ActiveStep"/> is <c>null</c>)
/// * The previous step in this Playbook executed successfully (<see cref="PlaybookRunProperties.ActiveStep"/> refers to the successful step)
/// The only thing this activity does is update <see cref="PlaybookRunProperties.ActiveStep"/> so it correctly refers to the next step, OR is <c>null</c> if no further steps are to be executed.
/// </remarks>
public class AdvancePlaybookActivity : IStateMachineActivity<PlaybookRun>
{
    readonly ILogger<AdvancePlaybookActivity> _logger;

    public AdvancePlaybookActivity(ILogger<AdvancePlaybookActivity> logger)
    {
        _logger = logger;
    }

    public void Probe(ProbeContext context)
    {
    }

    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);

    public async Task Execute(BehaviorContext<PlaybookRun> context, IBehavior<PlaybookRun> next)
    {
        AdvancePlaybook(context);
        await next.Execute(context);
    }

    public async Task Execute<T>(BehaviorContext<PlaybookRun, T> context, IBehavior<PlaybookRun, T> next) where T : class
    {
        AdvancePlaybook(context);
        await next.Execute(context);
    }

    public Task Faulted<TException>(BehaviorExceptionContext<PlaybookRun, TException> context, IBehavior<PlaybookRun> next) where TException : Exception =>
        next.Faulted(context);

    public Task Faulted<T, TException>(BehaviorExceptionContext<PlaybookRun, T, TException> context, IBehavior<PlaybookRun, T> next) where T : class where TException : Exception =>
        next.Faulted(context);

    void AdvancePlaybook(BehaviorContext<PlaybookRun> context)
    {
        var definition = context.GetPlaybookDefinition();
        var activeStep = context.Saga.Properties.ActiveStep;

        ActionReference? nextStep = null;
        string? activeSequence = null;
        if (activeStep is null)
        {
            activeSequence = definition.StartSequence;
            nextStep = SelectFirstStepInSequence(context, definition, definition.StartSequence);
        }
        else
        {
            activeSequence = activeStep.SequenceId;
            nextStep = SelectNextStepInSameSequence(definition, activeStep);
        }

        // Now that we have a candidate next step, check if we're calling a branch.
        // If we are, but the sequence name is null, then this is a no-op and we're not "calling" anything anyway.
        if (context.Saga.Properties.GetActiveStepResult() is { CallBranch: { SequenceName: { } nextSequence } })
        {
            // We are! Save our current next step, if there is one, on the stack.
            if (nextStep is not null)
            {
                _logger.PushedToStack(nextStep.SequenceId, nextStep.ActionId, nextStep.ActionIndex);
                context.Saga.Properties.PushFrame(new()
                {
                    Step = nextStep,
                });
            }

            _logger.CallingSequence(nextSequence);

            // Change the next step to the first step in the sequence we're calling.
            activeSequence = nextSequence;
            nextStep = SelectFirstStepInSequence(context, definition, nextSequence);
        }

        // Now, if after all that we have no next step, then we've reached the end of a sequence.
        // It might even be that we "called" a sequence and it was empty!
        // When that happens, we're "returning" from the active sequences.
        if (nextStep is null)
        {
            _logger.ReachedEndOfSequence(activeSequence);

            // There is no next step.
            // Try to pop off the stack
            if (context.Saga.Properties.PopFrame() is { } nextFrame)
            {
                _logger.PoppedFromStack(nextFrame.Step.SequenceId, nextFrame.Step.ActionId, nextFrame.Step.ActionIndex);
                // There was a frame to pop off the stack.
                // Use it's step as the next step
                nextStep = nextFrame.Step;
            }

            // If the stack is empty, leave nextStep null, we're done with the playbook.
        }

        // Finally, we can "return" the active step.
        context.Saga.Properties.ActiveStep = nextStep;
    }

    ActionReference? SelectNextStepInSameSequence(PlaybookDefinition definition, ActionReference activeStep)
    {
        // Find the sequence
        if (!definition.Sequences.TryGetValue(activeStep.SequenceId, out var currentSequence))
        {
            // Validation should have checked this, but just in case...
            throw new UnreachableException(
                $"Active sequence '{activeStep.SequenceId}' no longer found in playbook definition");
        }

        var nextActionIndex = activeStep.ActionIndex + 1;

        // Is there a next step?
        if (currentSequence.Actions.Count > nextActionIndex)
        {
            var stepId = currentSequence.Actions[nextActionIndex].Id;
            _logger.AdvancingTo(activeStep.SequenceId, stepId, nextActionIndex);
            return new ActionReference(activeStep.SequenceId, stepId, nextActionIndex);
        }
        else
        {
            _logger.AdvancingToEndOfSequence(activeStep.SequenceId);
            return null;
        }
    }

    ActionReference? SelectFirstStepInSequence(
        BehaviorContext<PlaybookRun> context, PlaybookDefinition definition, string sequenceName)
    {
        // Identify the first step in the playbook and advance to it
        if (!definition.Sequences.TryGetValue(sequenceName, out var sequence))
        {
            // Validation should have checked this, but just in case...
            throw new UnreachableException(
                $"Sequence '{sequenceName}' not found in playbook definition");
        }

        var step = sequence.Actions.FirstOrDefault();
        // If step was null, we'll leave ActiveStep null and the activities that follow us should complete the playbook.
        if (step is not null)
        {
            _logger.AdvancingTo(sequenceName, step.Id, 0);
            return new(sequenceName, step.Id, 0);
        }
        else
        {
            _logger.AdvancingToEndOfSequence(sequenceName);
            return null;
        }
    }
}

public static partial class AdvancePlaybookActivityLoggingExtensions
{
    [LoggerMessage(
        Level = LogLevel.Information,
        EventId = 1,
        Message = "Advancing to {ActionId}#{ActionIndex} in sequence {SequenceName}")]
    public static partial void AdvancingTo(this ILogger<AdvancePlaybookActivity> logger, string sequenceName, string actionId, int actionIndex);

    [LoggerMessage(
        Level = LogLevel.Information,
        EventId = 2,
        Message = "Advancing to the end of sequence {SequenceName}.")]
    public static partial void AdvancingToEndOfSequence(this ILogger<AdvancePlaybookActivity> logger, string sequenceName);

    [LoggerMessage(
        Level = LogLevel.Information,
        EventId = 3,
        Message = "Pushing {SequenceName}/{ActionId}#{ActionIndex} to stack.")]
    public static partial void PushedToStack(this ILogger<AdvancePlaybookActivity> logger, string sequenceName, string actionId, int actionIndex);

    [LoggerMessage(
        Level = LogLevel.Information,
        EventId = 4,
        Message = "Calling Sequence {SequenceName}.")]
    public static partial void CallingSequence(this ILogger<AdvancePlaybookActivity> logger, string sequenceName);

    [LoggerMessage(
        Level = LogLevel.Information,
        EventId = 5,
        Message = "Reached end of sequence {SequenceName}.")]
    public static partial void ReachedEndOfSequence(this ILogger<AdvancePlaybookActivity> logger, string sequenceName);

    [LoggerMessage(
        Level = LogLevel.Information,
        EventId = 6,
        Message = "Popped {SequenceName}/{ActionId}#{ActionIndex} from stack.")]
    public static partial void PoppedFromStack(this ILogger<AdvancePlaybookActivity> logger, string sequenceName, string actionId, int actionIndex);
}
