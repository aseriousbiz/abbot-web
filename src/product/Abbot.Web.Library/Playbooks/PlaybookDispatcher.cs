using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messages;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Signals;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Used to dispatch playbooks.
/// </summary>
public class PlaybookDispatcher
{
    static readonly Counter<int> PlaybookGroupCountMetric = AbbotTelemetry.Meter.CreateCounter<int>(
        "playbooks.dispatch.group.count",
        "dispatches",
        "The number of playbook groups dispatched, by trigger type");

    static readonly Counter<int> PlaybookRunCountMetric = AbbotTelemetry.Meter.CreateCounter<int>(
        "playbooks.dispatch.run.count",
        "dispatches",
        "The number of playbooks runs dispatched, by trigger type");

    static readonly Histogram<int> PlaybookRunGroupSize = AbbotTelemetry.Meter.CreateHistogram<int>(
        "playbooks.dispatch.group.size",
        "runs",
        "The number of playbook runs in each run group");

    readonly PlaybookRepository _playbookRepository;
    readonly StepTypeCatalog _stepTypeCatalog;
    readonly CustomerRepository _customerRepository;
    readonly IUserRepository _userRepository;
    readonly IPublishEndpoint _publishEndpoint;
    readonly FeatureService _featureService;
    readonly IAuditLog _auditLog;
    readonly ILogger<PlaybookDispatcher> _logger;

    public PlaybookDispatcher(
        PlaybookRepository playbookRepository,
        StepTypeCatalog stepTypeCatalog,
        CustomerRepository customerRepository,
        IUserRepository userRepository,
        IPublishEndpoint publishEndpoint,
        FeatureService featureService,
        IAuditLog auditLog,
        ILogger<PlaybookDispatcher> logger)
    {
        _playbookRepository = playbookRepository;
        _stepTypeCatalog = stepTypeCatalog;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
        _publishEndpoint = publishEndpoint;
        _featureService = featureService;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>
    /// Requests that the specified run be cancelled as soon as possible.
    /// </summary>
    /// <param name="run">The <see cref="PlaybookRun"/> representing the run to cancel.</param>
    /// <param name="actor">The <see cref="Member"/> requesting cancellation.</param>
    /// <param name="staffReason">If performed by staff, this is the reason for cancellation.</param>
    public async Task RequestCancellationAsync(PlaybookRun run, Member actor, string? staffReason = null)
    {
        await _publishEndpoint.Publish(new CancelPlaybook(run.CorrelationId, actor));
        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Playbook.Run", "CancellationRequested"),
                ParentIdentifier = run.Properties.RootAuditEventId,
                Actor = actor,
                Organization = run.Playbook.Organization,
                Description = $"Requested cancellation of playbook run `{run.CorrelationId}`",
                EntityId = run.Id,
                StaffPerformed = staffReason is not null,
                StaffReason = staffReason,
                Properties = PlaybookRepository.PlaybookRunLogProperties.FromPlaybookRun(run, null),
            },
            new(AnalyticsFeature.Playbooks, "Run cancellation requested")
            {
                ["Organization"] = run.Playbook.Organization.PlatformId,
                ["Staff"] = staffReason is not null,
            });
    }

    /// <summary>
    /// Dispatches all playbooks containing the specified trigger type to any playbooks that are listening for it.
    /// </summary>
    /// <param name="triggerType">The type of trigger to dispatch.</param>
    /// <param name="outputs">The outputs of the trigger.</param>
    /// <param name="organization">The organization the trigger belongs to.</param>
    /// <param name="relatedEntities">The playbook related entities.</param>
    public async Task DispatchAsync(
        string triggerType,
        IDictionary<string, object?> outputs,
        Organization organization,
        PlaybookRunRelatedEntities? relatedEntities = null)
    {
        using var orgScope = _logger.BeginOrganizationScope(organization);

        try
        {
            var publishedPlaybookVersions = await _playbookRepository.GetLatestPlaybookVersionsWithTriggerTypeAsync(
                triggerType,
                organization,
                includeDraft: false,
                includeDisabled: false);

            foreach (var version in publishedPlaybookVersions)
            {
                await DispatchAsync(version, triggerType, outputs, relatedEntities: relatedEntities);
            }
        }
        catch (Exception e)
        {
            _logger.ErrorDuringDispatch(e, triggerType);
        }
    }

    /// <summary>
    /// Dispatches the specified playbook with the specified outputs.
    /// </summary>
    public async Task<PlaybookRunGroup?> DispatchAsync(
        PlaybookVersion version,
        string triggerType,
        IDictionary<string, object?> outputs,
        HttpTriggerRequest? triggerRequest = null,
        SignalMessage? signal = null,
        Member? actor = null,
        PlaybookRunRelatedEntities? relatedEntities = null)
    {
        using var playbookScope = _logger.BeginPlaybookScope(version.Playbook);
        using var playbookVersionScope = _logger.BeginPlaybookVersionScope(version);

        // Never dispatch for disabled orgs
        if (!version.Playbook.Organization.Enabled)
        {
            _logger.SkippingDispatchForDisabledOrg();
            return null;
        }

        // This should never happen, but never can be too careful.
        if (version.PublishedAt is null)
        {
            _logger.PlaybookVersionNotPublished();
            return null;
        }

        try
        {
            var triggers = PlaybookFormat.Deserialize(version.SerializedDefinition)
                .Triggers
                .Where(t => t.Type == triggerType)
                .ToList();

            if (triggers is [])
            {
                _logger.TriggerNoLongerExists(version, triggerType);
                return null;
            }

            var trigger = triggers
                .FirstOrDefault(t => ShouldTrigger(t, outputs));

            if (trigger is null)
            {
                return null;
            }

            var definition = PlaybookFormat.Deserialize(version.SerializedDefinition);
            if (PlaybookFormat.Validate(definition) is { Count: > 0 })
            {
                throw new InvalidOperationException("Attempting to run an invalid playbook.");
            }

            return await DispatchAsync(
                version,
                definition,
                outputs,
                trigger,
                triggerRequest,
                signal,
                actor,
                relatedEntities);
        }
        catch (Exception e)
        {
            _logger.ErrorDuringDispatch(e, triggerType);
            return null;
        }
    }

    bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs)
    {
        if (!_stepTypeCatalog.TryGetTriggerType(triggerStep.Type, out var triggerType))
        {
            _logger.TriggerStepTypeMissing(triggerStep.Id, triggerStep.Type);
            return false;
        }

        if (!triggerType.ShouldTrigger(triggerStep, outputs, out var reason))
        {
            _logger.TriggerConditionNotSatisfied(triggerStep.Id, triggerStep.Type, reason);
            return false;
        }

        _logger.TriggerConditionSatisfied(triggerStep.Id, triggerStep.Type, reason);
        return true;
    }

    async Task<PlaybookRunGroup> DispatchAsync(
        PlaybookVersion version,
        PlaybookDefinition definition,
        IDictionary<string, object?> outputs,
        TriggerStep trigger,
        HttpTriggerRequest? triggerRequest,
        SignalMessage? signal,
        Member? actor,
        PlaybookRunRelatedEntities? relatedEntities)
    {
        actor ??= await _userRepository.EnsureAbbotMemberAsync(version.Playbook.Organization);

        var canMultiDispatch =
            await _featureService.IsEnabledAsync(FeatureFlags.PlaybookDispatching, version.Playbook.Organization);

        var dispatchSettings = canMultiDispatch
            ? definition.Dispatch
            : DispatchSettings.Default;

        // Create a run group for the run(s)
        var group = await _playbookRepository.CreateRunGroupAsync(version, dispatchSettings, trigger, actor);

        // It might make sense, later on, to migrate this process to it's own MassTransit Saga.
        // That Saga would control the entire Playbook Run Group lifecycle.
        // So instead of iterating here, we'd publish a `ExecutePlaybookRunGroup` message and let the Saga handle it.
        // The Saga would handle things like group-wide cancellation, collecting completion status, etc.

        var metricTags = AbbotTelemetry.CreateOrganizationTags(group.Playbook.Organization);
        metricTags.Add("TriggerType", trigger.Type);

        if (definition.Dispatch.Type == DispatchType.Once)
        {
            var dispatchContext = new DispatchContext
            {
                Type = DispatchType.Once
            };

            if (relatedEntities is not null)
            {
                // Multiple dispatches may reuse this instance, so force a new instance per run
                relatedEntities = relatedEntities with { };
            }

            PlaybookRunGroupSize.Record(1, metricTags);
            await DispatchSingleRunAsync(
                metricTags,
                group,
                version,
                dispatchContext,
                outputs,
                trigger,
                actor,
                triggerRequest,
                signal,
                relatedEntities);
        }
        else if (definition.Dispatch.Type == DispatchType.ByCustomer)
        {
            await DispatchByCustomerAsync(
                metricTags,
                group,
                version,
                outputs,
                trigger,
                actor,
                triggerRequest,
                signal,
                relatedEntities,
                definition.Dispatch.CustomerSegments.ToReadOnlyList());
        }
        else
        {
            throw new UnreachableException($"Unknown dispatch type: {definition.Dispatch.Type}.");
        }
        return group;
    }

    async Task DispatchByCustomerAsync(
        TagList metricTags,
        PlaybookRunGroup group,
        PlaybookVersion version,
        IDictionary<string, object?> outputs,
        TriggerStep trigger,
        Member actor,
        HttpTriggerRequest? triggerRequest,
        SignalMessage? signal,
        PlaybookRunRelatedEntities? relatedEntities,
        IReadOnlyList<string> segments)
    {
        // Fetch all customers for the organization
        var customers = await _customerRepository.GetAllWithRoomsAsync(group.Playbook.Organization, segments);

        // Iterate over them triggering runs
        var dispatchCount = 0;
        foreach (var customer in customers)
        {
            // Update trigger outputs with the customer.
            var runOutputs = new OutputsBuilder(outputs);
            runOutputs.SetCustomer(customer);

            // Update related entities.
            // It's possible there are _other_ related entities in the original dispatch that we won't update.
            relatedEntities = (relatedEntities ?? new()) with
            {
                Customer = customer,
            };

            // Dispatch this run!
            try
            {
                await DispatchSingleRunAsync(
                    metricTags,
                    group,
                    version,
                    new DispatchContext()
                    {
                        Type = DispatchType.ByCustomer,
                        EntityId = customer.Id,
                        EntityName = customer.Name,
                    },
                    runOutputs.Outputs,
                    trigger,
                    actor,
                    triggerRequest,
                    signal,
                    relatedEntities);

                dispatchCount += 1;
            }
            catch (Exception ex)
            {
                _logger.FailedToDispatchRun(ex, customer, customer.Name);
                // Don't let one failed dispatch stop the rest.
            }
        }
        PlaybookRunGroupSize.Record(dispatchCount, metricTags);
        PlaybookGroupCountMetric.Add(1, metricTags);

        // Update the group to indicate the dispatch count
        await _playbookRepository.CompleteRunGroupDispatchAsync(group, dispatchCount);
    }

    async Task DispatchSingleRunAsync(
        TagList metricTags,
        PlaybookRunGroup group,
        PlaybookVersion version,
        DispatchContext dispatchContext,
        IDictionary<string, object?> outputs,
        TriggerStep trigger,
        Member actor,
        HttpTriggerRequest? triggerRequest,
        SignalMessage? signal,
        PlaybookRunRelatedEntities? relatedEntities)
    {
        // This method assumes any trigger conditions have already been checked.
        if (!_stepTypeCatalog.TryGetTriggerType(trigger.Type, out _))
        {
            _logger.TriggerStepTypeMissing(trigger.Id, trigger.Type);
            return;
        }

        try
        {
            var run = await _playbookRepository.CreateRunAsync(group,
                version,
                dispatchContext,
                trigger,
                outputs,
                actor,
                triggerRequest,
                signal,
                relatedEntities);

            await _publishEndpoint.Publish(
                new ExecutePlaybook(
                    run.CorrelationId,
                    trigger.Id,
                    version,
                    version.Playbook,
                    version.Playbook.Organization));

            _logger.DispatchedRun(run.CorrelationId.ToString(), group.CorrelationId.ToString());

            metricTags.SetSuccess();
            PlaybookRunCountMetric.Add(1, metricTags);
        }
        catch (Exception e)
        {
            _logger.ErrorDuringTriggerStepDispatch(e,
                trigger.Type,
                trigger.Id,
                dispatchContext.Type,
                dispatchContext.EntityId);

            metricTags.SetFailure(e);
            PlaybookRunCountMetric.Add(1, metricTags);
        }
    }

    [DisplayName("Run Scheduled Trigger `{2}` for Playbook (Id: {0}) (Version: {1}) - Scheduled by Member Id: {3}")]
    public async Task DispatchScheduledRunAsync(
        Id<Playbook> playbookId,
        int version,
        string triggerId,
        Id<Member>? actorId = default)
    {
        if (await _playbookRepository.GetAsync(playbookId) is not { } playbook)
        {
            // The playbook has since been deleted.
            _logger.PlaybookNotFound(playbookId);
            return;
        }

        if (await _playbookRepository.GetCurrentVersionAsync(playbook, includeDraft: false, includeDisabled: false) is
            not { } publishedVersion)
        {
            _logger.NoPublishedVersionOfPlaybook(playbookId);
            return;
        }

        if (publishedVersion.Version != version)
        {
            _logger.RequestedVersionNotLatestPublished(playbookId, version);
            return;
        }

        using var orgScope = _logger.BeginOrganizationScope(playbook.Organization);
        using var playbookScope = _logger.BeginPlaybookScope(playbook);
        using var playbookVersionScope = _logger.BeginPlaybookVersionScope(publishedVersion);

        // Never dispatch for disabled orgs
        if (!playbook.Organization.Enabled)
        {
            _logger.SkippingDispatchForDisabledOrg();
            return;
        }

        // Confirm trigger still exists.
        var definition = PlaybookFormat.Deserialize(publishedVersion.SerializedDefinition);
        if (PlaybookFormat.Validate(definition) is { Count: > 0 })
        {
            throw new InvalidOperationException("Attempting to run an invalid playbook.");
        }

        // Since we confirmed we're on the same version, we don't need to check the type.
        // But we do it anyways.
        var trigger = definition
            .Triggers
            .Where(t => t.Type is ScheduleTrigger.Id)
            .FirstOrDefault(t => t.Id == triggerId);

        if (trigger is null)
        {
            // This trigger is gone!
            _logger.TriggerNotFound(triggerId);
            return;
        }

        var actor = actorId is null
            ? null
            : await _userRepository.GetMemberByIdAsync(actorId.Value);

        await DispatchAsync(
            publishedVersion,
            definition,
            outputs: new Dictionary<string, object?>(),
            trigger,
            triggerRequest: null,
            signal: null,
            actor,
            relatedEntities: null);
    }

    /// <summary>
    /// Dispatches the signal to any playbooks that are listening for it.
    /// </summary>
    /// <param name="signal">The signal.</param>
    /// <param name="outputs">The outputs for the signal.</param>
    /// <param name="organization">The organization the signal was raised in.</param>
    public async Task DispatchSignalAsync(
        SignalMessage signal,
        IDictionary<string, object?> outputs,
        Organization organization)
    {
        using var orgScope = _logger.BeginOrganizationScope(organization);

        if (signal.Name.StartsWith(SystemSignal.Prefix, StringComparison.Ordinal))
        {
            // System signals are not dispatched to playbooks.
            return;
        }

        try
        {
            var publishedPlaybookVersions = await _playbookRepository.GetLatestPlaybookVersionsWithTriggerTypeAsync(
                SignalTrigger.Id,
                organization,
                includeDraft: false,
                includeDisabled: false);

            foreach (var version in publishedPlaybookVersions)
            {
                using var playbookScope = _logger.BeginPlaybookScope(version.Playbook);
                using var playbookVersionScope = _logger.BeginPlaybookVersionScope(version);

                var definition = PlaybookFormat.Deserialize(version.SerializedDefinition);
                if (PlaybookFormat.Validate(definition) is { Count: > 0 })
                {
                    throw new InvalidOperationException("Attempting to run an invalid playbook.");
                }
                var signalTrigger = definition.Triggers.FirstOrDefault(t =>
                    t.Type is SignalTrigger.Id
                    && t.Inputs.TryGetValue("signal", out var signalName)
                    && signalName as string == signal.Name);

                if (signalTrigger is not null)
                {
                    // This playbook is subscribed to this signal!
                    await DispatchAsync(
                        version,
                        definition,
                        outputs,
                        signalTrigger,
                        triggerRequest: null,
                        signal,
                        actor: null,
                        relatedEntities: null);
                }
            }
        }
        catch (Exception e)
        {
            _logger.ErrorDuringDispatch(e, SignalTrigger.Id);
        }
    }
}

static partial class PlaybookTriggerDispatcherLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Trigger Step Type does not exist {TriggerId} {TriggerType}.")]
    public static partial void TriggerStepTypeMissing(
        this ILogger<PlaybookDispatcher> logger,
        string triggerId,
        string triggerType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Trigger condition not satisfied {TriggerId} {TriggerType}. Reason: {TriggerReason}")]
    public static partial void TriggerConditionNotSatisfied(
        this ILogger<PlaybookDispatcher> logger,
        string triggerId,
        string triggerType,
        string triggerReason);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message =
            "Somehow we have a playbook version {PlaybookVersionId} with no matching trigger type {triggerType}.")]
    public static partial void TriggerNoLongerExists(
        this ILogger<PlaybookDispatcher> logger,
        Id<PlaybookVersion> playbookVersionId,
        string triggerType);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Exception attempting to dispatch {TriggerType} playbook trigger")]
    public static partial void ErrorDuringDispatch(
        this ILogger<PlaybookDispatcher> logger, Exception exception, string triggerType);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Error,
        Message =
            "Exception attempting to dispatch {TriggerType} step {TriggerStepId} playbook trigger. (DispatchType: {DispatchType}, EntityId: {EntityId})")]
    public static partial void ErrorDuringTriggerStepDispatch(
        this ILogger<PlaybookDispatcher> logger, Exception exception, string triggerType, string triggerStepId,
        DispatchType dispatchType, int? entityId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Playbook version not published")]
    public static partial void PlaybookVersionNotPublished(
        this ILogger<PlaybookDispatcher> logger);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Skipping dispatch for disabled org")]
    public static partial void SkippingDispatchForDisabledOrg(
        this ILogger<PlaybookDispatcher> logger);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Error,
        Message = "Failed to dispatch run for customer {CustomerName} ({CustomerId})")]
    public static partial void FailedToDispatchRun(
        this ILogger<PlaybookDispatcher> logger, Exception ex, Id<Customer> customerId, string customerName);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "Dispatched Run {PlaybookRunId} in Group {PlaybookRunGroupId}")]
    public static partial void DispatchedRun(
        this ILogger<PlaybookDispatcher> logger, string playbookRunId, string playbookRunGroupId);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Error,
        Message = "Playbook {PlaybookId} not found")]
    public static partial void PlaybookNotFound(this ILogger<PlaybookDispatcher> logger, Id<Playbook> playbookId);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Error,
        Message = "Playbook {PlaybookId} has no published version")]
    public static partial void NoPublishedVersionOfPlaybook(this ILogger<PlaybookDispatcher> logger, Id<Playbook> playbookId);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "Version {PlaybookVersion} of playbook {PlaybookId} is not the latest published version.")]
    public static partial void RequestedVersionNotLatestPublished(this ILogger<PlaybookDispatcher> logger, Id<Playbook> playbookId, int playbookVersion);

    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Error,
        Message = "Trigger {TriggerId} not found")]
    public static partial void TriggerNotFound(this ILogger<PlaybookDispatcher> logger,
        string triggerId);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Information,
        Message = "Trigger condition satisfied {TriggerId} {TriggerType}: {TriggerReason}")]
    public static partial void TriggerConditionSatisfied(
        this ILogger<PlaybookDispatcher> logger,
        string triggerId,
        string triggerType,
        string triggerReason);
}
