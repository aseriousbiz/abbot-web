using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using Cronos;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messages;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Serialization;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using TimeZoneConverter;

namespace Serious.Abbot.Repositories;

public class PlaybookRepository
{
    readonly AbbotContext _db;
    readonly IAuditLog _auditLog;
    readonly IClock _clock;
    readonly Histogram<long> _upcomingEventComputeDuration;
    readonly Histogram<long> _upcomingEventPlaybookCount;
    readonly Histogram<long> _upcomingEventPlaybookRunCount;
    readonly Histogram<long> _upcomingEventCount;

    public PlaybookRepository(AbbotContext db, IAuditLog auditLog, IClock clock)
    {
        _db = db;
        _auditLog = auditLog;
        _clock = clock;

        _upcomingEventComputeDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
            "playbooks.upcomingEvents.duration",
            "milliseconds",
            "How long it took to compute upcoming events for an organization");

        _upcomingEventPlaybookCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "playbooks.upcomingEvents.playbookCount",
            "playbooks",
            "The number of playbooks fetched to compute upcoming events");

        _upcomingEventPlaybookRunCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "playbooks.upcomingEvents.playbookRunCount",
            "playbook runs",
            "The number of playbook runs fetched to compute upcoming events");

        _upcomingEventCount = AbbotTelemetry.Meter.CreateHistogram<long>(
            "playbooks.upcomingEvents.count",
            "events",
            "The number of upcoming events computed");
    }

    IQueryable<PlaybookRunGroup> DefaultRunGroupsQuery => _db.PlaybookRunGroups
        .Include(r => r.Playbook.Organization);

    IQueryable<PlaybookRun> DefaultRunsQuery => _db.PlaybookRuns
        .Include(r => r.Playbook.Organization)
        .Include(r => r.Related!.Conversation!.Organization)
        .Include(r => r.Related!.Conversation!.StartedBy.User)
        .Include(r => r.Related!.Conversation!.Room)
        .Include(r => r.Related!.Customer!.Organization)
        .Include(r => r.Related!.Room!.Organization);

    IQueryable<PlaybookRunGroup> GetDefaultRunGroupsQuery(Id<Organization> organizationId) =>
        DefaultRunGroupsQuery.Where(r => r.Playbook.OrganizationId == organizationId);

    IQueryable<PlaybookRun> GetDefaultRunsQuery(Id<Organization> organizationId)
        => DefaultRunsQuery.Where(r => r.Playbook.OrganizationId == organizationId);

    public async Task<EntityResult<Playbook>> CreateAsync(string name, string? description, string slug, bool enabled,
        Member actor, string? staffReason = null)
    {
        var playbook = new Playbook
        {
            Name = name,
            Slug = slug,
            Description = description,
            Enabled = enabled,
            Organization = actor.Organization,
            OrganizationId = actor.OrganizationId,
        };

        await _db.Playbooks.AddAsync(playbook);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.GetDatabaseError() is UniqueConstraintError
        {
            ConstraintName: "IX_Playbooks_OrganizationId_Slug"
        })
        {
            return EntityResult.Conflict<Playbook>($"A playbook with the slug {slug} already exists.");
        }

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new AuditEventType("Playbook", AuditOperation.Created),
                Description = $"Playbook `{name}` created.",
                Actor = actor,
                Organization = actor.Organization,
                EntityId = playbook.Id,
                StaffPerformed = staffReason is not null,
                StaffReason = staffReason,
                Properties = PlaybookLogProperties.FromPlaybook(playbook, null),
            },
            new(AnalyticsFeature.Playbooks, "Playbook created"));

        return EntityResult.Success(playbook);
    }

    public async Task<Playbook> DeleteAsync(Playbook playbook, Member actor)
    {
        _db.Playbooks.Remove(playbook);
        var versions = await _db.PlaybookVersions
            .TagWithCallSite()
            .Where(pv => pv.PlaybookId == playbook.Id).ToListAsync();

        _db.PlaybookVersions.RemoveRange(versions);
        await _db.SaveChangesAsync();

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new AuditEventType("Playbook", AuditOperation.Removed),
                Description = $"Playbook `{playbook.Name}` deleted.",
                Actor = actor,
                Organization = actor.Organization,
                EntityId = playbook.Id,
                Properties = PlaybookLogProperties.FromPlaybook(playbook, null),
            },
            new(AnalyticsFeature.Playbooks, "Playbook deleted"));

        return playbook;
    }

    /// <summary>
    /// Updates the latest version of a playbook. If the latest version is published, this creates a new unpublished
    /// version.
    /// </summary>
    /// <param name="playbook">The <see cref="Playbook"/> to update.</param>
    /// <param name="definition">The updated definition.</param>
    /// <param name="actor">The <see cref="Member"/> making the change.</param>
    /// <returns>The updated <see cref="PlaybookVersion"/>.</returns>
    /// <exception cref="ValidationException">Thrown for an invalid playbook definition.</exception>
    /// <exception cref="ArgumentException">If there are duplicate trigger Ids.</exception>
    public async Task<PlaybookVersion> UpdateLatestVersionAsync(
        Playbook playbook,
        string definition,
        Member actor)
    {
        var jsonDefinition = PlaybookFormat.Deserialize(definition);
        if (PlaybookFormat.Validate(jsonDefinition) is { Count: > 0 })
        {
            // It's not our job to extract specific validation errors here.
            throw new ValidationException("Invalid playbook definition");
        }

        if (jsonDefinition.Triggers.GroupBy(t => t.Id).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Duplicate trigger IDs are not allowed.");
        }

        var latestVersion = await GetCurrentVersionAsync(playbook, includeDraft: true, includeDisabled: true);

        if (latestVersion is not { PublishedAt: null })
        {
            // Published versions are immutable.
            return await CreateVersionAsync(
                playbook,
                definition,
                comment: null,
                actor);
        }

        // Unpublished versions can be updated.
        latestVersion.SerializedDefinition = definition;
        await _db.SaveChangesAsync();

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new AuditEventType("Playbook.Version", AuditOperation.Changed),
                Description = $"Playbook `{playbook.Name}` version `{latestVersion.Version}` draft updated.",
                Actor = actor,
                Organization = actor.Organization,
                EntityId = playbook.Id,
                Properties = PlaybookLogProperties.FromPlaybook(playbook, jsonDefinition),
            },
            new(AnalyticsFeature.Playbooks, "Version updated")
            {
                ["draft"] = true,
            });

        return latestVersion;
    }

    public async Task<PlaybookVersion> CreateVersionAsync(
        Playbook playbook,
        string definition,
        string? comment,
        Member actor,
        string? staffReason = null)
    {
        var jsonDefinition = PlaybookFormat.Deserialize(definition);

        if (jsonDefinition.Triggers.GroupBy(t => t.Id).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Duplicate trigger IDs are not allowed.");
        }

        var playbookVersion = new PlaybookVersion
        {
            PlaybookId = playbook.Id,
            Playbook = playbook,
            Comment = comment,
            Version = await GetMaxVersionAsync(playbook) + 1,
            PublishedAt = null,
            CreatorId = actor.User.Id,
            Creator = actor.User,
            ModifiedById = actor.User.Id,
            ModifiedBy = actor.User,
            SerializedDefinition = definition,
        };

        await _db.PlaybookVersions.AddAsync(playbookVersion);
        await _db.SaveChangesAsync();

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new AuditEventType("Playbook", AuditOperation.Created),
                Description = $"Playbook `{playbook.Name}` version `{playbookVersion.Version}` draft created.",
                Actor = actor,
                Organization = actor.Organization,
                EntityId = playbook.Id,
                StaffPerformed = staffReason is not null,
                StaffReason = staffReason,
                Properties = PlaybookLogProperties.FromPlaybook(playbook, jsonDefinition),
            },
            new(AnalyticsFeature.Playbooks, "Version created")
            {
                ["draft"] = true,
            });

        return playbookVersion;
    }

    public async Task<IPaginatedList<PlaybookRunGroupSummary>> GetRunGroupsAsync(
        Id<Organization> organizationId, string playbookSlug, int pageNumber, int pageSize)
    {
        var query = GetDefaultRunGroupsQuery(organizationId)
            .TagWithCallSite()
            .Where(r => r.Playbook.OrganizationId == organizationId && r.Playbook.Slug == playbookSlug)
            .SelectRunGroupSummary()
            .OrderByDescending(r => r.Group.Id);

        return await PaginatedList.CreateAsync(query, pageNumber, pageSize);
    }

    public async Task<IPaginatedList<PlaybookVersion>> GetAllVersionsAsync(Id<Playbook> playbookId, int pageNumber,
        int pageSize)
    {
        var query = _db.PlaybookVersions
            .TagWithCallSite()
            .Where(r => r.PlaybookId == playbookId)
            .Include(r => r.Playbook)
            .Include(pv => pv.Creator)
            .Include(pv => pv.ModifiedBy)
            .OrderByDescending(r => r.Version);

        return await PaginatedList.CreateAsync(query, pageNumber, pageSize);
    }

    async Task<int> GetMaxVersionAsync(Id<Playbook> playbookId)
    {
        return await _db.PlaybookVersions.Where(pv => pv.PlaybookId == playbookId).MaxAsync(v => (int?)v.Version) ?? 0;
    }

    public async Task CompleteRunGroupDispatchAsync(PlaybookRunGroup group, int totalDispatchCount)
    {
        group.Properties.TotalDispatchCount = totalDispatchCount;
        _db.PlaybookRunGroups.Update(group);
        await _db.SaveChangesAsync();
    }

    public async Task<PlaybookRunGroup> CreateRunGroupAsync(
        PlaybookVersion version,
        DispatchSettings dispatchSettings,
        TriggerStep trigger,
        Member actor)
    {
        var group = new PlaybookRunGroup
        {
            CorrelationId = Guid.NewGuid(),
            Playbook = version.Playbook,
            Version = version.Version,
            Properties = new()
            {
                DispatchType = dispatchSettings.Type,
                DispatchSettings = dispatchSettings,
                TriggerType = trigger.Type,
                Trigger = trigger.Id,
            },
            CreatedBy = actor,
        };

        await _db.AddAsync(group);
        await _db.SaveChangesAsync();

        var evt = await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new AuditEventType("Playbook.RunGroup", AuditOperation.Created),
                Description =
                    $"Ran `{version.Playbook.Name}` version `{version.Version}`.",
                Details =
                    $"Started run group `{group.CorrelationId}` of playbook `{version.Playbook.Name}` from trigger `{trigger.Type}`.",
                Actor = actor,
                Organization = version.Playbook.Organization,
                EntityId = group.Id,
            },
            new(AnalyticsFeature.Playbooks, "Run Group started")
            {
                ["trigger_type"] = trigger.Type,
            });

        // We want the root audit event ID in the run ID so we can attach child events to it
        group.Properties.RootAuditEventId = evt.Identifier;
        _db.PlaybookRunGroups.Update(group);
        await _db.SaveChangesAsync();
        return group;
    }

    public async Task<PlaybookRun> CreateRunAsync(
        PlaybookRunGroup group,
        PlaybookVersion version,
        DispatchContext dispatchContext,
        TriggerStep trigger,
        IDictionary<string, object?> outputs,
        Member actor,
        HttpTriggerRequest? triggerRequest = null,
        SignalMessage? signal = null,
        PlaybookRunRelatedEntities? relatedEntities = null)
    {
        var playbook = version.Playbook;

        var run = new PlaybookRun
        {
            CorrelationId = Guid.NewGuid(),
            Playbook = group.Playbook,
            Version = group.Version,
            Group = group,
            SerializedDefinition = version.SerializedDefinition,
            State = "Initial",
            Related = relatedEntities,
            Properties = new PlaybookRunProperties
            {
                DispatchContext = dispatchContext,
                ActivityId = Activity.Current?.Id ?? "<unknown>",
                StepResults = new Dictionary<string, StepResult>
                {
                    [trigger.Id] = new(StepOutcome.Succeeded)
                    {
                        Outputs = outputs,
                    }
                },
                Trigger = trigger.Id,
                TriggerRequest = triggerRequest,
                SignalMessage = signal,
            },
        };

        await _db.AddAsync(run);
        await _db.SaveChangesAsync();

        var (description, detail) = dispatchContext.GetAuditDescription() is { } desc
            ? (
                $"Running playbook `{playbook.Name}` version `{version.Version}` {desc}",
                $"Started run `{run.CorrelationId}` of playbook `{run.Playbook.Name}` {desc} from trigger `{trigger.Type}`."
            )
            : (
                $"Running playbook `{playbook.Name}` version `{version.Version}`",
                $"Started run `{run.CorrelationId}` of playbook `{run.Playbook.Name}` from trigger `{trigger.Type}`."
            );

        var evt = await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new AuditEventType("Playbook.Run", AuditOperation.Created),
                IsTopLevel = false,
                ParentIdentifier = group.Properties.RootAuditEventId,
                Description = description,
                Details = detail,
                Actor = actor,
                Organization = playbook.Organization,
                EntityId = run.Id,
                Properties = PlaybookRunLogProperties.FromPlaybookRun(run, trigger),
            },
            new(AnalyticsFeature.Playbooks, "Run started")
            {
                ["trigger_type"] = trigger.Type,
            });

        // We want the root audit event ID in the run ID so we can attach child events to it
        run.Properties.RootAuditEventId = evt.Identifier;
        _db.PlaybookRuns.Update(run);
        await _db.SaveChangesAsync();

        return run;
    }

    public async Task<Playbook?> GetBySlugAsync(string slug, Organization organization)
    {
        return await GetQueryable(organization)
            .TagWithCallSite()
            .SingleOrDefaultAsync(p => p.Slug.ToLower() == slug.ToLower());
    }

    public async Task<Playbook?> GetAsync(Id<Playbook> id)
    {
        return await Entities.TagWithCallSite().SingleOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IPaginatedList<Playbook>> GetAllAsync(
        Organization organization,
        int pageNumber,
        int pageSize)
    {
        var query = GetQueryable(organization)
            .TagWithCallSite();

        return await PaginatedList.CreateAsync(query, pageNumber, pageSize);
    }

    IQueryable<Playbook> Entities => _db.Playbooks.Include(p => p.Organization);

    IQueryable<Playbook> GetQueryable(Id<Organization> organizationId)
    {
        return Entities
            .Where(p => p.OrganizationId == organizationId);
    }

    public async Task<PlaybookVersion?> GetPlaybookVersionAsync(Id<Organization> organizationId, string slug,
        int versionNumber)
    {
        return await _db.PlaybookVersions
            .TagWithCallSite()
            .Include(pv => pv.Creator)
            .Include(pv => pv.ModifiedBy)
            .Include(pv => pv.Playbook)
            .ThenInclude(p => p.Organization)
            .Where(pv => pv.Playbook.OrganizationId == organizationId && pv.Playbook.Slug == slug
                                                                      && pv.Version == versionNumber)
            .SingleOrDefaultAsync();
    }

    public Task<PlaybookVersion?> GetCurrentVersionAsync(Id<Playbook> playbookId, bool includeDraft,
        bool includeDisabled)
    {
        return _db.PlaybookVersions
            .TagWithCallSite()
            .Include(pv => pv.Creator)
            .Include(pv => pv.ModifiedBy)
            .Include(pv => pv.Playbook)
            .ThenInclude(p => p.Organization)
            .Where(pv => pv.PlaybookId == playbookId)
            .Where(pv => includeDraft || pv.PublishedAt != null)
            .Where(pv => includeDisabled || pv.Playbook.Enabled)
            .OrderByDescending(pv => pv.Version)
            .FirstOrDefaultAsync();
    }

    public async Task<IPaginatedList<PlaybookViewModel>> GetIndexAsync(
        Organization organization,
        string? filter,
        int pageNumber,
        int pageSize)
    {
        var queryable = _db.Playbooks
            .TagWithCallSite()
            .Include(p => p.Organization)
            .Where(p => p.OrganizationId == organization.Id);

        if (filter is { Length: > 0 } filterString)
        {
            queryable = queryable
                .Where(p => p.Name.ToLower().Contains(filterString.ToLower())
                || p.Description!.ToLower().Contains(filterString.ToLower()));
        }

        return await PaginatedList.CreateAsync(
            queryable
                .OrderBy(p => p.Name)
                .Select(p => new {
                    Playbook = p, // Load whole Playbook so it's available in LastRun
                    p.Organization,
                    Current = p.Versions
                        .OrderByDescending(v => v.Version).FirstOrDefault(),
                    Published = p.Versions
                        .OrderByDescending(v => v.Version)
                        .FirstOrDefault(v => v.PublishedAt != null),
                    LastRunGroup = p.RunGroups
                        .OrderByDescending(r => r.Id)
                        // Alas, SelectRunGroupSummary doesn't work here because we are _inside_ an expression.
                        .Select(g => new PlaybookRunGroupSummary()
                        {
                            Group = g,
                            LatestRun = g.Runs.OrderByDescending(r => r.CompletedAt ?? r.StartedAt ?? r.Created).FirstOrDefault(),
                        })
                        .FirstOrDefault()
                }),
            pageNumber,
            pageSize,
            p => PlaybookViewModel.FromPlaybook(
                p.Playbook,
                p.Current,
                p.Published,
                p.LastRunGroup));
    }

    /// <summary>
    /// Retrieve all latest playbook versions with a trigger type. This helps us discover playbooks with a
    /// specific trigger type such as signal triggers, etc.
    /// </summary>
    /// <param name="triggerType">The type of trigger.</param>
    /// <param name="organization">The organization the playbook belongs to.</param>
    /// <param name="includeDraft">Whether or not to include draft versions.</param>
    /// <param name="includeDisabled">Whether or not to include disabled playbooks.</param>
    /// <returns>A list of the latest playbook version per playbook that contains the trigger type.</returns>
    public async Task<IReadOnlyList<PlaybookVersion>> GetLatestPlaybookVersionsWithTriggerTypeAsync(
        string triggerType,
        Organization organization,
        bool includeDraft,
        bool includeDisabled)
    {
        if (!organization.Enabled)
        {
            return Array.Empty<PlaybookVersion>();
        }

        var initialQuery = _db.Playbooks
            .TagWithCallSite()
            .Where(p => includeDisabled || p.Enabled);

        // We only want the latest playbook version for each playbook that matches our criteria.
        var query = includeDraft
            ? initialQuery.SelectMany(p => p.Versions.OrderByDescending(pv => pv.Version).Take(1))
            : initialQuery.SelectMany(p =>
                p.Versions.Where(pv => pv.PublishedAt != null).OrderByDescending(pv => pv.Version).Take(1));

        query = query
            .Include(pv => pv.Playbook)
            .ThenInclude(p => p.Organization)
            .Where(pv => pv.Playbook.OrganizationId == organization.Id);

        var versions = await query.ToListAsync();

        return versions
            .Where(pv => PlaybookFormat.Deserialize(pv.SerializedDefinition).Triggers.Any(t => t.Type == triggerType))
            .ToReadOnlyList();
    }

    public async Task<PlaybookRun?> GetRunAsync(Guid playbookRunId)
    {
        return await DefaultRunsQuery
            .TagWithCallSite()
            .Include(r => r.Playbook.Organization)
            .Include(r => r.Group)
            .Where(r => r.CorrelationId == playbookRunId)
            .SingleOrDefaultAsync();
    }

    public async Task<PlaybookRunGroup?> GetRunGroupAsync(Guid playbookRunGroupId)
    {
        return await DefaultRunGroupsQuery
            .TagWithCallSite()
            .Where(r => r.CorrelationId == playbookRunGroupId)
            .Include(g => g.Runs)
            .FirstOrDefaultAsync();
    }

    public record PlaybookLogProperties(
        string Name,
        string Slug,
        string? Description,
        PlaybookProperties? Properties,
        PlaybookDefinition? PlaybookDefinition)
    {
        public static PlaybookLogProperties FromPlaybook(Playbook playbook, PlaybookDefinition? playbookDefinition)
            => new(playbook.Name,
                playbook.Slug,
                playbook.Description,
                playbook.Properties,
                playbookDefinition);
    }

    public record PlaybookRunLogProperties(
        string Name,
        string Slug,
        int Version,
        DispatchContext? DispatchContext,
        TriggerStep? Trigger,
        Guid CorrelationId,
        Guid? GroupCorrelationId,
        string RunState,
        PlaybookRunOutcome? RunOutcome,
        PlaybookProperties? PlaybookProperties,
        PlaybookRunProperties? RunProperties,
        PlaybookDefinition? PlaybookDefinition)
    {
        public static PlaybookRunLogProperties FromPlaybookRun(PlaybookRun run, TriggerStep? trigger) =>
            new(run.Playbook.Name,
                run.Playbook.Slug,
                run.Version,
                run.Properties.DispatchContext,
                trigger,
                run.CorrelationId,
                run.Group?.CorrelationId,
                run.State,
                run.Properties.Result?.Outcome,
                run.Playbook.Properties,
                run.Properties,
                PlaybookFormat.Deserialize(run.SerializedDefinition));
    }

    public async Task<PlaybookVersion> SetPublishedVersionAsync(PlaybookVersion currentVersion, Member actor)
    {
        currentVersion.PublishedAt = _clock.UtcNow;
        await _db.SaveChangesAsync();
        await _auditLog.LogAuditEventAsync(
            new AuditEventBuilder
            {
                Type = new AuditEventType("Playbook.Version", AuditOperation.Changed),
                Description =
                    $"Playbook `{currentVersion.Playbook.Name}` version `{currentVersion.Version}` published.",
                Actor = actor,
                Organization = actor.Organization,
                EntityId = currentVersion.Playbook.Id,
                Properties = PlaybookLogProperties.FromPlaybook(
                    currentVersion.Playbook,
                    PlaybookFormat.Deserialize(currentVersion.SerializedDefinition)),
            },
            new(AnalyticsFeature.Playbooks, "Version published"));

        return currentVersion;
    }

    public async Task UpdatePlaybookAsync(Playbook playbook, string name, string? description, Member actor)
    {
        playbook.Name = name;
        playbook.Description = description;
        await _auditLog.LogAuditEventAsync(new AuditEventBuilder
        {
            Type = new AuditEventType("Playbook", AuditOperation.Changed),
            Description = $"Playbook `{playbook.Name}` settings updated.",
            Actor = actor,
            Organization = actor.Organization,
            EntityId = playbook.Id,
            Properties = PlaybookLogProperties.FromPlaybook(
                playbook,
                null),
        });

        await _db.SaveChangesAsync();
    }

    public async Task UpdateRunGroupAsync(PlaybookRunGroup group, Member actor)
    {
        _db.PlaybookRunGroups.Update(group);
        await _db.SaveChangesAsync();
    }

    public async Task SetPlaybookEnabledAsync(Playbook playbook, bool enabled, Member actor)
    {
        playbook.Enabled = enabled;
        var (action, eventName) = enabled
            ? ("enabled", "Enabled")
            : ("disabled", "Disabled");

        await _auditLog.LogAuditEventAsync(
            new()
            {
                Type = new AuditEventType("Playbook", eventName),
                Description = $"Playbook `{playbook.Name}` {action}.",
                Actor = actor,
                Organization = actor.Organization,
                EntityId = playbook.Id,
                Properties = PlaybookLogProperties.FromPlaybook(
                    playbook,
                    null),
            },
            new(AnalyticsFeature.Playbooks, $"Playbook {action}"));


        await _db.SaveChangesAsync();
    }

    public async Task<PlaybookRunGroupSummary?> GetLatestRunGroupAsync(Playbook playbook)
    {
        return await GetDefaultRunGroupsQuery(playbook.Organization)
            .Where(g => g.PlaybookId == playbook.Id)
            .OrderByDescending(r => r.Id)
            .SelectRunGroupSummary()
            .FirstOrDefaultAsync();
    }

    public async Task<PlaybookRun?> GetLatestRunAsync(Playbook playbook)
    {
        return await GetDefaultRunsQuery(playbook.Organization)
            .Where(r => r.PlaybookId == playbook.Id)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Gets a list of <see cref="UpcomingPlaybookEvent"/>s representing the next <paramref name="count"/> upcoming
    /// playbook events (scheduled runs, resumes, etc.)
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to fetch events for.</param>
    /// <param name="count">The maximum number of events to return.</param>
    /// <param name="occurrencesPerScheduledPlaybook">The maximum number of occurrences to compute for a scheduled playbook.</param>
    public async Task<IReadOnlyList<UpcomingPlaybookEvent>> GetUpcomingEventsAsync(
        Organization organization,
        int count,
        int occurrencesPerScheduledPlaybook)
    {
        return await GetUpcomingEventsCoreAsync(
            organization,
            count,
            occurrencesPerScheduledPlaybook,
            runQueryFilter: q => q,
            scheduledPlaybookFilter: (_, _) => true);
    }

    /// <summary>
    /// Gets a list of <see cref="UpcomingPlaybookEvent"/>s representing the next <paramref name="count"/> upcoming
    /// playbook events (scheduled runs, resumes, etc.) for the provided customer.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to fetch events for.</param>
    /// <param name="count">The maximum number of events to return.</param>
    /// <param name="occurrencesPerScheduledPlaybook">The maximum number of occurrences to compute for a scheduled playbook.</param>
    /// <param name="customerId">Filter only to events that may involve the provided customer.</param>
    public async Task<IReadOnlyList<UpcomingPlaybookEvent>> GetUpcomingEventsAsync(
        Organization organization,
        int count,
        int occurrencesPerScheduledPlaybook,
        Id<Customer> customerId)
    {
        return await GetUpcomingEventsCoreAsync(
            organization,
            count,
            occurrencesPerScheduledPlaybook,

            // Show only runs related to this customer.
            runQueryFilter: q => q.Where(r => r.Related!.CustomerId == customerId.Value),

            // Assume anything dispatched per customer is running for this customer
            scheduledPlaybookFilter: (_, d) => d.Dispatch.Type == DispatchType.ByCustomer);
    }

    async Task<IReadOnlyList<UpcomingPlaybookEvent>> GetUpcomingEventsCoreAsync(
        Organization organization,
        int count,
        int occurrencesPerScheduledPlaybook,
        Func<IQueryable<PlaybookRun>, IQueryable<PlaybookRun>> runQueryFilter,
        Func<PlaybookVersion, PlaybookDefinition, bool> scheduledPlaybookFilter)
    {
        IEnumerable<UpcomingPlaybookEvent> ComputeNextRunTimes(PlaybookVersion playbookVersion)
        {
            var definition = PlaybookFormat.Deserialize(playbookVersion.SerializedDefinition);
            if (!scheduledPlaybookFilter(playbookVersion, definition))
            {
                yield break;
            }
            var schedules = new List<(CronExpression Cron, TimeZoneInfo Timezone)>();
            foreach (var scheduleTrigger in definition.Triggers.Where(t => t.Type == ScheduleTrigger.Id))
            {
                var timezone = scheduleTrigger.Inputs.TryGetValue("tz", out var tz)
                    ? tz as string
                    : null;

                var schedule = AbbotJsonFormat.Default.Convert<Schedule>(
                    scheduleTrigger.Inputs.TryGetValue("schedule", out var v)
                        ? v
                        : null);

                if (schedule is not null)
                {
                    var cron = CronExpression.Parse(schedule.ToCronString());
                    schedules.Add((cron, TZConvert.GetTimeZoneInfo(timezone ?? "UTC")));
                }
            }

            var outputted = 0;
            // Prevent infinite loops, only try count * 2 times.
            DateTime startTime = _clock.UtcNow;
            for (int attempts = 0; outputted < occurrencesPerScheduledPlaybook && attempts < occurrencesPerScheduledPlaybook * 2; attempts++)
            {
                // Yep, we're modifying a value captured here outside the closure.
                // But `Min` runs in-line and doesn't hold on to the closure, so it's fine.
                // ReSharper disable once AccessToModifiedClosure
                var nextRunTime = schedules.Min((s) => s.Cron.GetNextOccurrence(startTime, s.Timezone));
                if (nextRunTime is not null)
                {
                    outputted++;
                    startTime = nextRunTime.Value;
                    yield return new()
                    {
                        Type = UpcomingPlaybookEventType.ScheduledDispatch,
                        Playbook = playbookVersion.Playbook,
                        Version = playbookVersion.Version,
                        ExpectedTime = nextRunTime,
                    };
                }
            }
        }

        var metricTags = AbbotTelemetry.CreateOrganizationTags(organization);
        using var _ = _upcomingEventComputeDuration.Time(metricTags);

        // Fetch playbooks with the Scheduled trigger.
        var scheduledPlaybooks = await GetLatestPlaybookVersionsWithTriggerTypeAsync(
            ScheduleTrigger.Id,
            organization,
            includeDraft: false,
            includeDisabled: false);
        _upcomingEventPlaybookCount.Record(scheduledPlaybooks.Count, metricTags);

        // Find the next run for each playbook.
        var nextScheduledRuns = scheduledPlaybooks
            .SelectMany(ComputeNextRunTimes)
            .ToList();

        // Fetch suspended run groups
        var suspendedRuns = await runQueryFilter(GetDefaultRunsQuery(organization))
            .Where(r => r.State == "Suspended")
            .ToListAsync();
        _upcomingEventPlaybookRunCount.Record(suspendedRuns.Count, metricTags);

        var upcomingEvents = suspendedRuns
            .Select(r =>
                new UpcomingPlaybookEvent
                {
                    Type = UpcomingPlaybookEventType.Resume,
                    Playbook = r.Playbook,
                    Version = r.Version,
                    PlaybookRun = r,
                    ExpectedTime = r.Properties.SuspendedUntil,
                })
            .Concat(nextScheduledRuns)
            // Sort by ascending time, with null values at the beginning (they represent events that will occur from some outside trigger)
            .OrderBy(e => e.ExpectedTime ?? DateTime.MinValue)
            .Take(count)
            .ToList();
        _upcomingEventCount.Record(upcomingEvents.Count, metricTags);

        return upcomingEvents;
    }

    public async Task<IPaginatedList<PlaybookRun>> GetRunsAsync(
        Id<Organization> organizationId,
        Id<Customer> customerId,
        int pageNumber,
        int pageSize)
    {
        var query = _db.PlaybookRuns
            .TagWithCallSite()
            .Where(r => r.Playbook.OrganizationId == organizationId && r.Related!.CustomerId == customerId)
            .Include(r => r.Playbook)
            .OrderByDescending(r => r.Id);

        return await PaginatedList.CreateAsync(query, pageNumber, pageSize);
    }
}
