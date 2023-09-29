using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Keeps the Bot's channel membership up to date.
/// </summary>
public class BotMembershipJob : IRecurringJob
{
    static readonly ILogger<BotMembershipJob> Log = ApplicationLoggerFactory.CreateLogger<BotMembershipJob>();

    readonly AbbotContext _db;
    readonly IOrganizationApiSyncer _organizationApiSyncer;
    readonly IClock _clock;

    public BotMembershipJob(AbbotContext db, IOrganizationApiSyncer organizationApiSyncer, IClock clock)
    {
        _db = db;
        _organizationApiSyncer = organizationApiSyncer;
        _clock = clock;
    }

    public static string Name => "Update Bot Room Membership";

    /// <summary>
    /// Queries the Slack API and updates the bot membership status.
    /// </summary>
    [Queue(HangfireQueueNames.Maintenance)]
    [DisableConcurrentExecution(timeoutInSeconds: 10)] // Only allow one instance to be running at a time.
    [AutomaticRetry(Attempts = 0)] // We don't want this job to retry. It'll run again on its next scheduled time.
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        // Grab all organizations using conversation management.
        var organizations = await _db.Organizations
            .Include(o => o.Rooms)
            .Include(o => o.Integrations)
            .Where(o => o.ApiToken != null)
            .Where(o => o.PlanType == PlanType.Business || o.Trial != null && o.Trial.Expiry > _clock.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var organization in organizations)
        {
            try
            {
                await _organizationApiSyncer.UpdateRoomsFromApiAsync(organization);
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception e)
            {
                Log.FailedToUpdateOrganizationRooms(e, organization.Id, organization.PlatformId);
            }
        }
    }
}

public static partial class BotMembershipJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Failed to update organization {OrganizationId} ({PlatformId}) rooms from API.")]
    public static partial void FailedToUpdateOrganizationRooms(
        this ILogger logger,
        Exception exception,
        int organizationId,
        string platformId);
}
