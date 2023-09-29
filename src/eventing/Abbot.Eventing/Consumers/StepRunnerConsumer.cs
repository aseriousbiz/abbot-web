using System.Diagnostics;
using System.Diagnostics.Metrics;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Infrastructure;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Eventing;

public class StepRunnerConsumer : IConsumer<RunPlaybookStep>, IConsumer<CancelSuspendedStep>
{
    public class Definition : AbbotConsumerDefinition<StepRunnerConsumer>
    {
        public Definition()
        {
            RequireSession("step-runner-v2");
        }
    }

    static readonly ILogger<StepRunnerConsumer> Log = ApplicationLoggerFactory.CreateLogger<StepRunnerConsumer>();

    static readonly Histogram<long> StepDurationMetric =
        AbbotTelemetry.Meter.CreateHistogram<long>(
            "playbooks.step.duration",
            "milliseconds",
            "How long it takes to execute an individual step");
    static readonly Counter<int> StepCountMetric =
        AbbotTelemetry.Meter.CreateCounter<int>(
            "playbooks.step.count",
            "invocations",
            "The total count of steps executed, by step type");

    readonly ActionDispatcher _actionDispatcher;
    readonly IOrganizationRepository _organizationRepository;
    readonly IStopwatchFactory _stopwatchFactory;
    readonly IAuditLog _auditLog;
    readonly IServiceProvider _services;

    public StepRunnerConsumer(
        ActionDispatcher actionDispatcher,
        IOrganizationRepository organizationRepository,
        IStopwatchFactory stopwatchFactory,
        IAuditLog auditLog,
        IServiceProvider services)
    {
        _actionDispatcher = actionDispatcher;
        _organizationRepository = organizationRepository;
        _stopwatchFactory = stopwatchFactory;
        _auditLog = auditLog;

        // Consumers are run in their own DI scope, which is also appropriate for running a Playbook Action.
        _services = services;
    }

    public async Task Consume(ConsumeContext<RunPlaybookStep> context)
    {
        var run = context.GetPayload<PlaybookRun>();
        using var stepScope = Log.BeginStepScope(context.Message.Step);
        var abbot = await _organizationRepository.EnsureAbbotMember(run.Playbook.Organization);

        var definition = PlaybookFormat.Deserialize(run.SerializedDefinition);
        var actionReference = context.Message.Step;
        Expect.True(definition.TryGetAction(actionReference, out var actionStep));

        Log.ExecutingStep(actionStep.Type, context.Message.Step.ActionId);

        var templateContext = CreateTemplateContext(run);

        var metricTags = AbbotTelemetry.CreateOrganizationTags(run.Playbook.Organization);
        metricTags.Add("StepType", actionStep.Type);
        var templateEvaluator = new TemplateEvaluator(templateContext, metricTags);

        // Generate the inputs
        var inputs = ComputeInputs(templateEvaluator, actionStep);

        // Can't replace this with "StepDurationMetric.Time" because we need to update and pass the tags.
        var stopwatch = _stopwatchFactory.StartNew();
        Exception? fault = null;
        StepResult? result = null;
        try
        {
            result = await GetExecutionResult(context, run, actionReference, actionStep, inputs, templateEvaluator, abbot);

            Log.StepExecuted(actionStep.Type, context.Message.Step.ActionId, result.Outcome);

            await context.RespondAsync(new PlaybookStepComplete(
                context.Message.PlaybookRunId,
                context.Message.Step,
                result));

            metricTags.SetSuccess();
        }
        catch (Exception ex)
        {
            fault = ex;
            metricTags.SetFailure(ex);
            throw;
        }
        finally
        {
            StepCountMetric.Add(1, metricTags);
            StepDurationMetric.Record(stopwatch.ElapsedMilliseconds, metricTags);

            // Log an audit event for the step execution, nested under the parent.
            if (run.Properties.RootAuditEventId is not null)
            {
                var outcome = result is null
                    ? "Fault"
                    : result.Outcome.ToString();
                await _auditLog.LogAuditEventAsync(
                    new()
                    {
                        IsTopLevel = false,
                        ParentIdentifier = run.Properties.RootAuditEventId,
                        Type = new("Playbook.Step", "Executed"),
                        Description = $"Executed `{actionStep.Type}` step `{actionStep.Id}` with outcome `{outcome}`",
                        Actor = abbot,
                        Organization = run.Playbook.Organization,
                        EntityId = run.Id,
                        Properties = new {
                            Step = actionStep,
                            Inputs = inputs,
                            Result = result,
                            Exception = fault,
                            Duration = stopwatch.ElapsedMilliseconds,
                        }
                    },
                    new(AnalyticsFeature.Playbooks, "Step executed")
                    {
                        ["step_type"] = actionStep.Type,
                        ["outcome"] = outcome,
                    });
            }
        }
    }

    public async Task Consume(ConsumeContext<CancelSuspendedStep> context)
    {
        var run = context.GetPayload<PlaybookRun>();
        var actionReference = context.Message.Step;
        using var stepScope = Log.BeginStepScope(actionReference);

        var definition = PlaybookFormat.Deserialize(run.SerializedDefinition);
        Expect.True(definition.TryGetAction(actionReference, out var actionStep));

        Log.CancellingSuspendedStep(actionStep.Type, actionReference.ActionId);

        if (!run.Properties.StepResults.TryGetValue(actionStep.Id, out var result))
        {
            // Didn't get far enough to need cleanup
            return;
        }

        if (result.Outcome is StepOutcome.Suspended or StepOutcome.Cancelled)
        {
            await _actionDispatcher.DisposeSuspendedStepAsync(_services,
                new(context)
                {
                    ActionReference = actionReference,
                    Step = actionStep,
                    Inputs = result.Inputs ?? new Dictionary<string, object?>(),
                    Playbook = run.Playbook,
                    PlaybookRun = run,
                    ResumeState = result.SuspendState,
                });
        }

        Log.SuspendedStepCancelled(actionStep.Type, actionReference.ActionId);

        // No response is needed, the playbook is already being marked as cancelled.
    }

    static IDictionary<string, object?> ComputeInputs(TemplateEvaluator templateEvaluator, ActionStep actionStep)
    {
        var outputInputs = new Dictionary<string, object?>();
        foreach (var input in actionStep.Inputs)
        {
            if (input.Value is string s)
            {
                outputInputs[input.Key] = templateEvaluator.Evaluate(s);
            }
            else
            {
                // TODO: Recursive templating on objects?
                outputInputs[input.Key] = input.Value;
            }

            // TODO: Validate and coerce inputs?
            // If we want Integer/Boolean/etc. inputs to be templatable, we need to allow them to accept strings.
            // So once the template has been rendered, we need to coerce the input to the correct type.
        }

        return outputInputs;
    }

    static InputTemplateContext CreateTemplateContext(PlaybookRun run)
    {
        // Build up the outputs dictionary
        var outputs = new Dictionary<string, object?>();

        var stepResults = run.Properties.StepResults
            .Select(p => new TemplateStepResult
            {
                Id = p.Key,
                Outcome = p.Value.Outcome,
                Problem = p.Value.Problem,
                Outputs = p.Value.Outputs,
            })
            .ToDictionary(p => p.Id);
        var trigger =
            run.Properties.Trigger is { } triggerId && stepResults.TryGetValue(triggerId, out var triggerResult)
                ? triggerResult
                : null;

        if (trigger is not null)
        {
            foreach (var output in trigger.Outputs)
            {
                outputs[output.Key] = output.Value;
            }
        }

        foreach (var stepId in run.Properties.CompletedSteps)
        {
            if (run.Properties.StepResults.TryGetValue(stepId.ActionId, out var stepResult))
            {
                foreach (var stepOutput in stepResult.Outputs)
                {
                    outputs[stepOutput.Key] = stepOutput.Value;
                }
            }
        }

        var previous =
            run.Properties.CompletedSteps.LastOrDefault() is { } lastStep &&
            stepResults.TryGetValue(lastStep.ActionId, out var lastStepResult)
                ? lastStepResult
                : null;

        return new()
        {
            Steps = stepResults,
            Outputs = outputs,
            Previous = previous,
            Trigger = trigger,
        };
    }

    async Task<StepResult> GetExecutionResult(
        ConsumeContext<RunPlaybookStep> context,
        PlaybookRun run,
        ActionReference actionReference,
        ActionStep step,
        IDictionary<string, object?> inputs,
        ITemplateEvaluator templateEvaluator,
        Member abbot)
    {
        // Check for suspend state for this step
        IDictionary<string, object?>? resumeState = null;
        if (run.Properties.StepResults.TryGetValue(step.Id, out var result) && result.Outcome == StepOutcome.Suspended)
        {
            // There is suspend state, so we're resuming this step.
            // The suspend state is now the resume state!
            resumeState = result.SuspendState;

            // Log that the playbook woke up to the audit log
            if (run.Properties.RootAuditEventId is not null)
            {
                await _auditLog.LogAuditEventAsync(
                    new()
                    {
                        IsTopLevel = false,
                        ParentIdentifier = run.Properties.RootAuditEventId,
                        Type = new("Playbook.Step", "Resumed"),
                        Description = $"Resumed `{step.Type}` step `{step.Id}`",
                        Actor = abbot,
                        Organization = run.Playbook.Organization,
                        EntityId = run.Id,
                        Properties = new {
                            Step = step,
                            Inputs = inputs,
                            result.SuspendState,
                        }
                    });
            }
        }

        using var activity = AbbotTelemetry.ActivitySource.StartActivity(
            $"Playbook Step {step.Type} {run.Playbook.Slug}/{step.Id}");

        try
        {
            var stepResult = await _actionDispatcher.ExecuteStepAsync(
                _services,
                new(context)
                {
                    ActionReference = actionReference,
                    Step = step,
                    Inputs = inputs,
                    Playbook = run.Playbook,
                    PlaybookRun = run,
                    ResumeState = resumeState,
                    TemplateEvaluator = templateEvaluator,
                });

            activity?.SetStatus(
                stepResult.Outcome switch
                {
                    StepOutcome.Failed => ActivityStatusCode.Error,
                    _ => ActivityStatusCode.Ok,
                },
                stepResult.Outcome is StepOutcome.Failed
                    ? stepResult.Problem?.Type
                    : stepResult.Outcome.ToString());

            stepResult.Inputs = inputs;
            return stepResult;
        }
        catch (Exception ex)
        {
            var problem = Problems.FromException(ex);

            activity?.SetStatus(ActivityStatusCode.Error, problem.Type);

            Log.StepError(ex, step.Type, context.Message.Step.ActionId);
            return new StepResult(StepOutcome.Failed)
            {
                Problem = problem,
                Inputs = inputs,
            };
        }
    }
}

static partial class StepRunnerConsumerLoggingExtensions
{
    static readonly Func<ILogger, int, string, string, IDisposable?> LoggerScope =
        LoggerMessage.DefineScope<int, string, string>("Step [{ActionIndex}] {SequenceId}/{ActionId}");

    public static IDisposable? BeginStepScope(this ILogger logger, ActionReference action) =>
        LoggerScope(logger, action.ActionIndex, action.SequenceId, action.ActionId);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Executing {ActionType} step {ActionId}.")]
    public static partial void ExecutingStep(this ILogger<StepRunnerConsumer> logger, string actionType, string actionId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Executed {ActionType} step {ActionId} with outcome '{Outcome}'.")]
    public static partial void StepExecuted(this ILogger<StepRunnerConsumer> logger, string actionType, string actionId, StepOutcome outcome);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Unexpected error executing {ActionType} step {ActionId}!")]
    public static partial void StepError(this ILogger<StepRunnerConsumer> logger, Exception ex, string actionType, string actionId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Cancelling suspended {ActionType} step {ActionId}.")]
    public static partial void CancellingSuspendedStep(this ILogger<StepRunnerConsumer> logger, string actionType, string actionId);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Cancelled suspended {ActionType} step {ActionId}.")]
    public static partial void SuspendedStepCancelled(this ILogger<StepRunnerConsumer> logger, string actionType, string actionId);
}
