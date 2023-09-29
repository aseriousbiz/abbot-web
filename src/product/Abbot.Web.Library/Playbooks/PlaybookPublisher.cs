using System.Linq;
using Hangfire;
using Hangfire.Common;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Serialization;
using TimeZoneConverter;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Class used to publish a playbook.
/// </summary>
public class PlaybookPublisher
{
    readonly PlaybookRepository _playbookRepository;
    readonly IRecurringJobManager _recurringJobManager;
    readonly ILogger<PlaybookPublisher> _logger;

    public PlaybookPublisher(PlaybookRepository playbookRepository, IRecurringJobManager recurringJobManager, ILogger<PlaybookPublisher> logger)
    {
        _playbookRepository = playbookRepository;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    /// <summary>
    /// Published the playbook and returns the published version.
    /// </summary>
    /// <remarks>
    /// If the current version is already published, this returns that version.
    /// If the current version is not published, this updates that version and sets it as published.
    /// </remarks>
    /// <param name="playbook">The playbook to publish.</param>
    /// <param name="actor">The <see cref="Member"/> publishing a playbook.</param>
    /// <returns>The published <see cref="PlaybookVersion"/>.</returns>
    public async Task<PlaybookVersion> PublishAsync(Playbook playbook, Member actor)
    {
        if (!actor.IsAgent())
        {
            throw new InvalidOperationException("Only agents can publish a Playbook.");
        }

        var currentVersion = await _playbookRepository
            .GetCurrentVersionAsync(playbook, includeDraft: true, includeDisabled: true)
            .Require("Playbook must have a current version before publishing it.");
        if (currentVersion.PublishedAt is not null)
        {
            // Current version is already published!
            return currentVersion;
        }

        var currentlyPublishedVersion = await _playbookRepository.GetCurrentVersionAsync(playbook, includeDraft: false, includeDisabled: true);

        // Later we may want to introduce a pattern similar to `IActionType` where `ITriggerType` defines both
        // Install and Uninstall methods that we run for any triggers that require additional processing when published.
        // For now we just hard-code these steps.
        UninstallPublishedTriggers(currentlyPublishedVersion);

        var newlyPublishedVersion = await _playbookRepository.SetPublishedVersionAsync(currentVersion, actor);

        InstallTriggers(newlyPublishedVersion, actor);

        return newlyPublishedVersion;
    }

    /// <summary>
    /// Sets the enabled state for a playbook, installing or uninstalling triggers as necessary.
    /// </summary>
    public async Task SetPlaybookEnabledAsync(Playbook playbook, bool enabled, Member actor)
    {
        // Update the DB first.
        // If we're disabling the playbook, this should immediately stop new triggers from firing.
        await _playbookRepository.SetPlaybookEnabledAsync(playbook, enabled, actor);

        var currentlyPublishedVersion =
            await _playbookRepository.GetCurrentVersionAsync(playbook, includeDraft: false, includeDisabled: true);

        if (enabled)
        {
            InstallTriggers(currentlyPublishedVersion, actor);
        }
        else
        {
            UninstallPublishedTriggers(currentlyPublishedVersion);
        }
    }

    void InstallTriggers(PlaybookVersion? currentVersion, Member actor)
    {
        if (currentVersion is null)
        {
            return;
        }

        var definition = PlaybookFormat.Deserialize(currentVersion.SerializedDefinition);
        foreach (var scheduleTrigger in definition.Triggers.Where(t => t.Type is ScheduleTrigger.Id))
        {
            var timezone = scheduleTrigger.Inputs.TryGetValue("tz", out var tz) ? tz as string : null;
            var schedule = AbbotJsonFormat.Default.Convert<Schedule>(scheduleTrigger.Inputs.TryGetValue("schedule", out var v) ? v : null);
            if (schedule is not null)
            {
                Id<Playbook> playbookId = currentVersion.Playbook;
                Id<Member> actorId = actor;
                var jobId =
                    GetRecurringJobId(currentVersion.Playbook, currentVersion.Version, scheduleTrigger);
                _logger.CreatingHangfireJob(jobId);
                _recurringJobManager.AddOrUpdate(
                    jobId,
                    Job.FromExpression<PlaybookDispatcher>(dispatcher
                        => dispatcher.DispatchScheduledRunAsync(
                            playbookId,
                            currentVersion.Version,
                            scheduleTrigger.Id,
                            actorId)),
                    schedule.ToCronString(),
                    TZConvert.GetTimeZoneInfo(timezone ?? "UTC"));
            }
        }
    }

    void UninstallPublishedTriggers(PlaybookVersion? currentlyPublishedVersion)
    {
        if (currentlyPublishedVersion is null)
        {
            return;
        }

        var publishedDefinition = PlaybookFormat.Deserialize(currentlyPublishedVersion.SerializedDefinition);
        foreach (var scheduleTrigger in publishedDefinition.Triggers.Where(t =>
                     t.Type is ScheduleTrigger.Id))
        {
            var jobId = GetRecurringJobId(
                currentlyPublishedVersion.Playbook,
                currentlyPublishedVersion.Version,
                scheduleTrigger);
            _logger.RemovingHangfireJob(jobId);
            _recurringJobManager.RemoveIfExists(jobId);
        }
    }

    static string GetRecurringJobId(Id<Playbook> playbookId, int version, Step trigger) =>
        $"Playbook_{playbookId}_{version}_{trigger.Id}";
}

public static partial class PlaybookPublisherLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Creating Hangfire Job {RecurringJobId}")]
    public static partial void CreatingHangfireJob(this ILogger<PlaybookPublisher> logger, string recurringJobId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Removing Hangfire Job {RecurringJobId}")]
    public static partial void RemovingHangfireJob(this ILogger<PlaybookPublisher> logger, string recurringJobId);
}
