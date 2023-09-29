using System.Diagnostics.Metrics;
using MassTransit;
using MassTransit.Scheduling;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Serialization;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Eventing.StateMachines;

public class PlaybookRunStateMachine : MassTransitStateMachine<PlaybookRun>
{
    static readonly Histogram<long> ExecutionLatencyMetric = AbbotTelemetry.Meter.CreateHistogram<long>(
        "playbooks.execution.latency",
        "milliseconds",
        "The time between when a playbook run is created and when it starts executing");
    static readonly Histogram<long> ExecutionDurationMetric = AbbotTelemetry.Meter.CreateHistogram<long>(
        "playbooks.execution.duration",
        "milliseconds",
        "The time between when a playbook run is created and when it starts executing");
    static readonly Counter<long> ExecutionCountMetric = AbbotTelemetry.Meter.CreateCounter<long>(
        "playbooks.execution.count",
        "executions",
        "The number of fully-executed playbook runs");
    static readonly Histogram<long> SuspendDurationMetric = AbbotTelemetry.Meter.CreateHistogram<long>(
        "playbooks.suspend.duration",
        "milliseconds",
        "The time between when a playbook run is suspended and when it resumed");
    static readonly Counter<long> TimeoutCountMetric = AbbotTelemetry.Meter.CreateCounter<long>(
        "playbooks.step.timeout.count",
        "timeouts",
        "The number of step timeouts that have occurred");
    static readonly Counter<long> ExecutionStepCountMetric = AbbotTelemetry.Meter.CreateCounter<long>(
        "playbooks.execution.steps.count",
        "steps",
        "The total number of steps executed in a playbook run");

    public class Definition : SagaDefinition<PlaybookRun>
    {
        readonly IServiceProvider _services;

        public Definition(IServiceProvider services)
        {
            _services = services;
            Endpoint(e => { e.Name = "playbook-runner-v2"; });
        }

        protected override void ConfigureSaga(IReceiveEndpointConfigurator endpointConfigurator,
            ISagaConfigurator<PlaybookRun> sagaConfigurator)
        {
            // Use the In-Memory Outbox.
            // This means that activities in the state machine that send messages will DEFER sending the message until the saga is persisted.
            // We want to transition the saga to the next state and THEN publish any resulting messages.
            // That way, if there's a concurrency failure transitioning the saga, we don't publish a message that we shouldn't have.
            //
            // In order to use the In-Memory Outbox, we need to ensure that the service scope is available _after_ the consumer (the Saga in this case) completes.
            // So we have to configure the endpoint to use Message Scoping, where each attempt to process a message of a given type gets a scope, instead of a single scope for each consumer.
            endpointConfigurator.UseMessageScope(_services);
            endpointConfigurator.UseInMemoryOutbox();

            if (endpointConfigurator is IServiceBusReceiveEndpointConfigurator sbConfigurator)
            {
                // Enable Message Session for the Saga's receive endpoint.
                // This should keep
                sbConfigurator.RequiresSession = true;
            }
        }
    }

    // WARNING: The State Machine is a singleton! So only singleton services can be injected into it.
    // If you need scoped services, you must create an Activity (see AdvancePlaybookActivity).
    // https://masstransit.io/documentation/patterns/saga/state-machine#state-machine
    public PlaybookRunStateMachine(IClock clock)
    {
        InstanceState(s => s.State);

        // Ignore this, it's only emitted if the unscheduling fails because the message has already been delivered.
        DuringAny(Ignore(CancelScheduledMessage));

        Request(
            () => RunStep,
            x => {
                // Steps have 5 minutes to run.
                // TODO: Configuration, even per-step, to allow for longer steps?
                x.Timeout = TimeSpan.FromMinutes(5);
            });

        // When the playbook run has just started...
        Initially(
            // And we're cancelled
            When(CancellationRequested)
                // Mark the cancellation request
                .Then(c => c.Saga.Properties.CancellationRequestedBy = c.Message.Canceller)
                // Just go straight to cancelled.
                .TransitionTo(Cancelled),
            // And we get a StartExecuting event.
            When(StartExecuting)
                .Then(c => {
                    c.Saga.StartedAt = clock.UtcNow;
                    ExecutionLatencyMetric.Record(
                        (long)(c.Saga.StartedAt.Value - c.Saga.Created).TotalMilliseconds);
                })
                // Execute the next step
                .TransitionTo(ExecuteNextStep));

        // When the playbook run has finished...
        During(Final,
            // We don't care about anything from here.
            Ignore(RunStep.TimeoutExpired),
            Ignore(ResumeExecuting),
            Ignore(CancellationRequested));

        During(Suspended,
            // We don't care about step timeouts.
            // Either it's from an earlier step and thus irrelevant,
            // or it's from the step we executed which suspended, which indicates the step ran successfully.
            Ignore(RunStep.TimeoutExpired),
            // If we're cancelled...
            When(CancellationRequested)
                // Mark the cancellation request
                .Then(c => c.Saga.Properties.CancellationRequestedBy = c.Message.Canceller)
                // Tell the StepRunnerConsumer to cancel the active step.
                .Publish(c =>
                    new CancelSuspendedStep(c.Saga.CorrelationId, c.Saga.Properties.ActiveStep.Require()))
                // Go cancel the playbook!
                .TransitionTo(Cancelled),
            // If we're resuming the active step...
            When(ResumeExecuting, c => c.Message.Step == c.Saga.Properties.ActiveStep)
                // ActiveStep is already set, so we can just fire another RunStep request to re-execute this step.
                .Then(c => {
                    if (c.Saga.Properties.LastSuspendTime is not null)
                    {
                        SuspendDurationMetric.Record((long)(clock.UtcNow - c.Saga.Properties.LastSuspendTime.Value)
                            .TotalMilliseconds);

                        c.Saga.Properties.LastSuspendTime = null;
                    }

                    var result = c.Saga.Properties.GetActiveStepResult().Require();
                    foreach (var kv in c.Message.SuspendState)
                    {
                        // MassTransit deserializes with Newtonsoft
                        // EF serialized with System.Text.Json
                        result.SuspendState[kv.Key] = JTokenStripper.StripJTokens(kv.Value);
                    }
                })
                .Request(
                    RunStep,
                    c => new RunPlaybookStep(
                        c.Saga.CorrelationId,
                        c.Saga.Properties.ActiveStep.Require()))
                .TransitionTo(RunStep.Pending));

        // When we're waiting on a step to complete...
        During(RunStep.Pending,
            // If we're cancelled...
            When(CancellationRequested)
                // Mark state as cancellation requested.
                // We can't complete the cancellation process until the current step completes.
                .Then(c => c.Saga.Properties.CancellationRequestedBy = c.Message.Canceller),

            When(RunStep.Completed)
                .Then(c => {
                    c.Saga.Properties.StepResults[c.Message.Step.ActionId] = StripJTokens(c.Message.Result);

                    static StepResult StripJTokens(StepResult result) =>
                        result with
                        {
                            Inputs = result.Inputs?.StripJTokens(),
                            Outputs = result.Outputs.StripJTokens(),
                        };
                }),

            // If we receive a successful result for the step we're waiting on
            When(RunStep.Completed,
                    e => e.Message.Step == e.Saga.Properties.ActiveStep
                         && e.Message.Result.Outcome == StepOutcome.Succeeded)
                // Record the result of the step
                .Then(c => {
                    c.Saga.Properties.CompletedSteps.Add(c.Message.Step);
                })
                // Execute the next step
                .TransitionTo(ExecuteNextStep),

            // If we receive a stopped result for the step we're waiting on
            When(RunStep.Completed,
                    e => e.Message.Step == e.Saga.Properties.ActiveStep
                         && e.Message.Result.Outcome == StepOutcome.CompletePlaybook)
                // Record the result of the step
                .Then(c => {
                    c.Saga.Properties.CompletedSteps.Add(c.Message.Step);
                })
                // End the playbook run.
                .TransitionTo(CompletedSuccessfully),

            // If we receive a suspend result for the step we're waiting on
            When(RunStep.Completed,
                    e => e.Message.Step == e.Saga.Properties.ActiveStep
                         && e.Message.Result.Outcome == StepOutcome.Suspended)
                // Record the step result
                .Then(c => {
                    c.Saga.Properties.LastSuspendTime = clock.UtcNow;
                    c.Saga.Properties.SuspendedUntil = c.Message.Result.SuspendedUntil;
                })
                // Suspend the playbook
                .TransitionTo(Suspended),

            // If we receive a failed result for the step we're waiting on
            When(RunStep.Completed,
                    e => e.Message.Step == e.Saga.Properties.ActiveStep
                         && e.Message.Result.Outcome == StepOutcome.Failed)
                // Record the failure and end the playbook run
                .Then(c => {
                    c.Saga.Properties.CompletedSteps.Add(c.Message.Step);
                    c.Saga.Properties.Result = new()
                    {
                        Outcome = PlaybookRunOutcome.Faulted,
                        Problem = c.Message.Result.Problem,
                    };
                })
                .Finalize(),

            // If the step we're waiting on faults
            When(RunStep.Faulted, e => e.Message.Message.Step == e.Saga.Properties.ActiveStep)
                .Then(c => {
                    // Because it's faulted, there's no StepResult to record.
                    c.Saga.Properties.CompletedSteps.Add(c.Message.Message.Step);
                    c.Saga.Properties.Result = new()
                    {
                        Outcome = PlaybookRunOutcome.Faulted,
                        Problem = Problems.FromException(c.Message.Exceptions.First()),
                    };
                })
                .Finalize(),

            // If the step we're waiting on times out
            // (The double '.Message' here is because the message we get is a TimeoutExpired and the original RunStep message is in _that message's_ Message property.)
            When(RunStep.TimeoutExpired, e => e.Message.Message.Require().Step == e.Saga.Properties.ActiveStep)
                // Record the timeout and end the playbook run
                .Then(c => {
                    TimeoutCountMetric.Add(1);
                    // Because there was a timeout, there's no StepResult to record.
                    c.Saga.Properties.CompletedSteps.Add(c.Message.Message.Require().Step);
                    c.Saga.Properties.Result = new()
                    {
                        Outcome = PlaybookRunOutcome.TimedOut,
                        Problem = null,
                    };
                })
                .Finalize());

        // Any time we enter the ExecuteStep state...
        WhenEnter(ExecuteNextStep,
            a => a
                .IfElse(
                    // If cancellation was requested...
                    condition: c => c.Saga.Properties.CancellationRequestedBy is not null,
                    // Go directly to the cancelled state.
                    thenActivityCallback: b => b.TransitionTo(Cancelled),
                    elseActivityCallback: b => b
                        // Advance to the next step in the playbook
                        .Activity(x => x.OfType<AdvancePlaybookActivity>())
                        .IfElse(
                            // Check if there is a next step.
                            condition: x => x.Saga.Properties.ActiveStep is not null,
                            thenActivityCallback: x =>
                                // Run the next step!
                                x.Request(
                                        RunStep,
                                        c => new RunPlaybookStep(
                                            c.Saga.CorrelationId,
                                            c.Saga.Properties.ActiveStep.Require()))
                                    .TransitionTo(RunStep.Pending),
                            elseActivityCallback: x =>
                                // No next step? The playbook is done!
                                x.TransitionTo(CompletedSuccessfully))));

        // Any time a playbook is completed successfully...
        WhenEnter(CompletedSuccessfully,
            a => a
                .Then(c =>
                    c.Saga.Properties.Result = new()
                    {
                        Outcome = PlaybookRunOutcome.Succeeded,
                        Problem = null,
                    })
                .Finalize());

        WhenEnter(Cancelled,
            a => a
                .Then(c => {
                    // If the active step is not finished, mark it cancelled
                    if (c.Saga.Properties.ActiveStep is { } activeStep)
                    {
                        var cancelledResult =
                            c.Saga.Properties.StepResults.TryGetValue(activeStep.ActionId, out var r)
                                ? r with { Outcome = StepOutcome.Cancelled }
                                : new StepResult(StepOutcome.Cancelled);

                        c.Saga.Properties.StepResults[activeStep.ActionId] = cancelledResult;

                        // Record the step as completed, if it's not already
                        if (!c.Saga.Properties.CompletedSteps.Contains(c.Saga.Properties.ActiveStep))
                        {
                            c.Saga.Properties.CompletedSteps.Add(c.Saga.Properties.ActiveStep);
                        }
                    }

                    // And mark the playbook cancelled.
                    c.Saga.Properties.Result = new()
                    {
                        Outcome = PlaybookRunOutcome.Cancelled,
                        Problem = null,
                    };
                })
                .Finalize());

        // When we get suspended
        WhenEnter(Suspended,
            a => a
                .If(
                    // If cancellation was requested...
                    c => c.Saga.Properties.CancellationRequestedBy is not null,
                    // Go directly to the cancelled state.
                    x => x
                        // Tell the StepRunnerConsumer to cancel the active step.
                        .Publish(c =>
                            new CancelSuspendedStep(c.Saga.CorrelationId, c.Saga.Properties.ActiveStep.Require()))
                        // Transition to the cancelled state.
                        .TransitionTo(Cancelled)));

        WhenEnter(Final,
            a => a
                .Then(c => {
                    c.Saga.CompletedAt = clock.UtcNow;

                    var metricTags = AbbotTelemetry.CreateOrganizationTags(c.Saga.Playbook.Organization);

                    if (c.Saga.StartedAt is not null)
                    {
                        // It might seem scary to store the duration of a playbook run as milliseconds.
                        // But the maximum 64-bit integer, when converted from milliseconds to years,
                        // can store 292,471,209 years, which should be fine :).
                        ExecutionDurationMetric.Record(
                            (long)(c.Saga.CompletedAt.Value - c.Saga.StartedAt.Value).TotalMilliseconds,
                            metricTags);
                    }

                    ExecutionStepCountMetric.Add(c.Saga.Properties.CompletedSteps.Count, metricTags);
                    ExecutionCountMetric.Add(1, metricTags);
                })
                // If we have any legacy runs without a Group specified, they won't publish their status. C'est la vie.
                .If(c => c.Saga.Group is not null,
                    x => x
                        .Publish(c => new PlaybookRunInGroupComplete(
                            c.Saga.Group.Require().CorrelationId, c.Saga.CorrelationId))));
    }

    public Event<ExecutePlaybook> StartExecuting { get; } = null!;

    public Event<ResumeSuspendedStep> ResumeExecuting { get; } = null!;

    public Event<CancelPlaybook> CancellationRequested { get; } = null!;

    public Event<CancelScheduledMessage> CancelScheduledMessage { get; } = null!;

    /// <summary>
    /// Intermediate state used to trigger the execution of the next step in the playbook.
    /// </summary>
    public State ExecuteNextStep { get; } = null!;

    /// <summary>
    /// The playbook has been suspended.
    /// </summary>
    public State Suspended { get; } = null!;

    /// <summary>
    /// Intermediate state used to trigger cancellation of the playbook.
    /// </summary>
    public State Cancelled { get; } = null!;

    /// <summary>
    /// Intermediate state used to trigger completion of the playbook.
    /// </summary>
    public State CompletedSuccessfully { get; } = null!;

    public Request<PlaybookRun, RunPlaybookStep, PlaybookStepComplete> RunStep { get; } = null!;
}

public static partial class PlaybookRunStateMachineLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Playbook state machine executing.")]
    public static partial void Executing(
        this ILogger<PlaybookRunStateMachine> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Playbook step {SequenceId}/{ActionId} executing.")]
    public static partial void StepExecuting(
        this ILogger<PlaybookRunStateMachine> logger, string sequenceId, string actionId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Playbook step {SequenceId}/{ActionId} executed with outcome '{Outcome}'.")]
    public static partial void StepExecuted(
        this ILogger<PlaybookRunStateMachine> logger, string sequenceId, string actionId, StepOutcome outcome);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Playbook step {SequenceId}/{ActionId} timeout.")]
    public static partial void StepTimeout(
        this ILogger<PlaybookRunStateMachine> logger, string sequenceId, string actionId);
}
