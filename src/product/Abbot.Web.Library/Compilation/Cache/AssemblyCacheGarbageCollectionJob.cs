using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Repositories;
using Serious.Abbot.Storage.FileShare;
using Serious.Logging;

namespace Serious.Abbot.Compilation;

/// <summary>
/// Class used to collect old outdated compiled assemblies in our assembly
/// cache, Azure File Share.
/// </summary>
public class AssemblyCacheGarbageCollectionJob : IRecurringJob
{
    const int ErrorThreshold = 5;

    static readonly ILogger<AssemblyCacheGarbageCollectionJob> Log =
        ApplicationLoggerFactory.CreateLogger<AssemblyCacheGarbageCollectionJob>();

    readonly IAssemblyCacheClient _assemblyCacheClient;
    readonly IOrganizationRepository _organizationRepository;

    public AssemblyCacheGarbageCollectionJob(
        IAssemblyCacheClient assemblyCacheClient,
        IOrganizationRepository organizationRepository)
    {
        _assemblyCacheClient = assemblyCacheClient;
        _organizationRepository = organizationRepository;
    }

    public static string Name => "AssemblyGarbageCollection";

    /// <summary>
    /// Deletes unused compiled assemblies.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <remarks>
    /// This grabs all the cache keys for skills in the system grouped by org. Then
    /// for each org, it deletes any cached assemblies that do not match the active
    /// cache keys and that have not been accessed in a while.
    /// </remarks>
    [AutomaticRetry(Attempts = 0)] // Better to just not garbage collect. We _also_ have alerts on storage usage.
    [Queue(HangfireQueueNames.Maintenance)]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await _organizationRepository.GetAllForGarbageCollectionAsync();
        Log.CollectingGarbage(organizations.Count);

        var errors = new List<Exception>();
        foreach (var organization in organizations.WhereNotNull())
        {
            try
            {
                await CollectAsync(organization);
            }
            catch (Exception ex)
            {
                // Don't let one failed org stop the job
                Log.ErrorCollectingGarbage(ex, organization.Id, organization.PlatformId);

                // But do let 'ErrorThreshold' failed orgs stop the job ;).
                errors.Add(ex);
                if (errors.Count > ErrorThreshold)
                {
                    throw new AggregateException("Abandoning Job. Too many errors.", errors);
                }
            }
        }
    }

    async Task CollectAsync(Organization organization)
    {
        var cacheKeys = organization
            .Skills
            .Where(s => s.Language == CodeLanguage.CSharp)
            .Select(s => s.CacheKey)
            .ToHashSet();

        var client = await _assemblyCacheClient.GetAssemblyCacheAsync(organization);
        if (client is null)
        {
            // No cache directory? No garbage!
            return;
        }

        Log.CollectingGarbageFor(organization.Id, organization.PlatformId);

        var assemblies = client.GetCachedAssemblies();

        var removed = 0;
        await foreach (var assembly in assemblies)
        {
            if (!cacheKeys.Contains(assembly.Name))
            {
                var lastAccessed = await assembly.GetDateLastAccessedAsync();
                if (lastAccessed < DateTimeOffset.UtcNow.Subtract(WebConstants.StaleAssemblyTimeSpan))
                {
                    // Only delete assemblies that haven't been accessed for 2 hours or more.
                    await assembly.DeleteIfExistsAsync();
                    removed++;
                }
            }
        }

        if (removed > 0)
        {
            Log.CollectedGarbage(organization.Id, organization.PlatformId, removed);
        }
    }
}

static partial class AssemblyCacheGarbageCollectionJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Collecting garbage for {OrganizationCount} organizations")]
    public static partial void CollectingGarbage(this ILogger<AssemblyCacheGarbageCollectionJob> logger,
        int organizationCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Debug,
        Message = "Collecting garbage for {OrganizationId}, {PlatformTeamId}")]
    public static partial void CollectingGarbageFor(this ILogger<AssemblyCacheGarbageCollectionJob> logger,
        int organizationId, string platformTeamId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Collected garbage for {OrganizationId}, {PlatformTeamId}. Removed {RemovedCount} assemblies")]
    public static partial void CollectedGarbage(this ILogger<AssemblyCacheGarbageCollectionJob> logger,
        int organizationId, string platformTeamId, int removedCount);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Error collecting garbage for {OrganizationId}, {PlatformTeamId}")]
    public static partial void ErrorCollectingGarbage(this ILogger<AssemblyCacheGarbageCollectionJob> logger,
        Exception ex, int organizationId, string platformTeamId);
}
