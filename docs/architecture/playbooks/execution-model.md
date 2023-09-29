# Playbook Execution Model

What happens when a playbook is run?
How are steps coordinated?
All of this and more will be answered here!

## Playbook Run State

A Playbook Run's state is tracked in the `PlaybookRunState` object.
This is an EF Core model, mapped to the `PlaybookRuns` table.
The `CorrelationId` is a unique key that is also carried in all messages related to the playbook run.
The `StepRunnerConsumer` and `PlaybookRunStateMachine` process messages for the same playbook run sequentially, though **independently** (meaning the `PlaybookRunStateMachine` and the `StepRunnerConsumer` can be simultaneously processing messages for the same run).
For example, while the `StepRunnerConsumer` is processing the execution of a step for run `A`, a `CancelPlaybook` message for the same run could be published.
The `PlaybookRunStateMachine` would process this message simultaneously with the `StepRunnerConsumer`'s execution of the step.

The `PlaybookRunStateMachine` is the _only_ component that should mutate the playbook run state once the run has begun.
The state machine is a [MassTransit Saga](https://masstransit.io/documentation/patterns/saga).
MassTransit handles automatically loading the playbook state from the database before processing a new message, and automatically saves the state back to the database.
It also handles concurrency, using either Optimistic or Pessimistic concurrency to ensure that only one message for a given playbook run can be processed at a time.

## Lifecycle of a Playbook Run

The state machine used in production can be viewed on the [Bus Topology Staff Tool](https://app.ab.bot/staff/bus) (staff login required).

### 1. `Initial` state - Dispatching

Useful types:

* `PlaybookDispatcher`
* `PlaybookRunState`
* `PlaybookRunStateMachine`

The first step in the lifecycle of a Playbook Run is Dispatching.
The `PlaybookDispatcher` is responsible for this step.
When a triggering condition occurs, the code for that condition calls one of the dispatch methods on `PlaybookDispatcher`.
These methods perform some validation, and then dispatch the playbook.

To dispatch the playbook, the dispatcher:

1. Creates a `PlaybookRunState` that encodes the initial state of the playbook run, and *captures the PlaybookDefinition* itself (to ensure a run always uses the same definition), including the outputs from the trigger
2. Publishes the `ExecutePlaybook` message to the Message Bus, referencing the correlation ID of the newly-created run state.

The `ExecutePlaybook` message is received by the `PlaybookRunStateMachine`.
The machine starts in the `Initial` state, and when it receives an `ExecutePlaybook` message, it immediately transitions to the `ExecuteNextStep` state.

### 2. `ExecuteNextStep` - Executing a Step

Useful types:

* `StepRunnerConsumer`
* `ActionDispatcher`
* `ActionExecutor`
* `IActionType`

Upon entering the `ExecuteNextStep` state, the state machine:

1. Runs `AdvancePlaybookActivity`, which updates `PlaybookRunStateProperties.ActiveStep` to point at the next step to be executed, or `null` if the playbook has concluded.
2. If `ActiveStep is not null`, the state machine:
   1. Transitions its state to `RunStep.Pending`.
   2. Publishes the `RunPlaybookStep` message, which includes a reference to the active step.
3. If `ActiveStep is null`, the state machine transitions to `CompletedSuccessfully` and completes the playbook (see below).

The `RunPlaybookStep` message is a [MassTransit "Request" message](https://masstransit.io/documentation/concepts/requests).
It is executed by the `StepRunnerConsumer`, which publishes a response message, `PlaybookStepComplete` when it finishes.
The state machine is waiting on this `PlaybookStepComplete` message to transition to the next step.

The `StepRunnerConsumer` evaluates the inputs provided in the Playbook Definition.
String inputs are evaluated as [Handlebars templates](https://handlebarsjs.com/) using the `InputTemplateContext` as a context object.
After preparing the inputs dictionary, the consumer calls the `ActionDispatcher` to lookup and invoke the `ActionExecutor` matching the `type` value specified for the step in the Playbook Definition.
The executor runs the logic for the step and returns a `StepResult`.
The `StepRunnerConsumer` packages the `StepResult` up in a `PlaybookStepComplete` message and publishes it to the bus, to be received by the state machine.

### 3. `RunStep.Pending` state - Completing a step

Upon receiving the `PlaybookStepComplete` message, we expect the state machine to be in the `RunStep.Pending` state.
The state machine immediately stores the `StepResult` in the run state.
What we do next depends on the `Outcome` property of the `StepResult` contained in the `PlaybookStepComplete` message.

* `Succeeded` - The step is added to the list of `CompletedSteps` on the run state, and we attempt to move to the next step, going back to section 2 above.
* `CompletePlaybook` - The step is added to the list of `CompletedSteps` on the run state, and we move directly to the `CompletedSuccessfully` state (see below).
* `Suspended` - We move directly to the `Suspended` state (see below).
* `Failed` - We record the failure in the run state and immediate transition to the `Final` state.

In this state, we're also listening for two other MassTransit-provided events:

* `RunStep.Faulted` is triggered if the `StepRunnerConsumer` throws on its last redelivery of a message. This indicates an infrastructure failure, so we immediately fail the playbook.
* `RunStep.TimeoutExpired` is triggered if the `StepRunnerConsumer` does not complete within 5 minutes. This indicates an infrastructure failure, so we immediately fail the playbook.

### 4. `Suspended` state - Suspending and resuming Playbook

If a step returns the `Suspended` outcome, the playbook is suspended.
We store the `SuspendState` returned by the playbook in the run state and move to the `Suspended` state.
In this state, the playbook can only be resumed by one of two events:

1. The `CancelPlaybook` message, which causes us to transition to `Cancelled`.
2. The `ResumePlaybook` message, which causes us to dispatch another `RunPlaybookStep` message and transition back to `RunStep.Pending` to wait for the result.

The state that returned the `Suspended` outcome is expected to have set up the necessary conditions so that the `ResumePlaybook` message will be published when the playbook should be resumed.

### 5. `CompletedSuccessfully` state - Wrapping things up

If we complete successfully, either by reaching the end of the playbook, or by a step returning `CompletePlaybook`, then we mark the result for the entire playbook in the run state, and transition to the `Final` state.

### 6. `Cancelled` state - Cancellation

The `CancelPlaybook` message is used to cancel a running playbook.
We make our best effort to cancel a playbook as quickly as possible, but **will not** interrupt a running step.
If the `CancelPlaybook` message arrives while the state machine is in the `RunStep.Pending` state (i.e. a step is executing) we mark the `CancellationRequestedBy` value in the run state, to signal that when the step completes, we should cancel the playbook.
If we are in the `Suspended` state, we publish the `CancelSuspendedStep` to allow the currently-suspended step to clean up and immediately mark the playbook as cancelled and transition to `Final`.

If a playbook is cancelled while it is suspended, we want to allow the step that suspended the playbook to clean up whatever resources it has allocated to resume the playbook.
For example, the "Wait" step schedules the `ResumePlaybook` message to be delivered when the wait duration elapses.
If this message is delivered after the playbook is cancelled, it will be ignored, but we'd like to remove the schedule if we can.

To do this, we publish the `CancelSuspendedStep` message, which is handled by the `StepRunnerConsumer`.
The consumer uses the `ActionDispatcher` to run any custom "disposal" logic the step has provided for when the playbook is cancelled.

### 7. `Final` state

Upon completion of the Playbook, we publish the `PlaybookRunComplete` message to allow consumers in other places in the system to observe the completion of the playbook.
The Playbook Run State is marked as in the `Final` state and should be immutable from that point on.
