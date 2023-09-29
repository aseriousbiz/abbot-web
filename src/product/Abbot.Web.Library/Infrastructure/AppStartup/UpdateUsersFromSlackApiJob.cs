using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Messaging;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Job to query for users in an organization and update them all via the Slack API.
/// </summary>
public class UpdateUsersFromSlackApiJob
{
    static readonly ILogger<UpdateUsersFromSlackApiJob> Log = ApplicationLoggerFactory.CreateLogger<UpdateUsersFromSlackApiJob>();

    readonly ISlackResolver _slackResolver;
    readonly AbbotContext _db;

    public UpdateUsersFromSlackApiJob(ISlackResolver slackResolver, AbbotContext db)
    {
        _slackResolver = slackResolver;
        _db = db;
    }

    [Queue(HangfireQueueNames.Maintenance)]
    public async Task UpdateUsersAsync(int orgId)
    {
        var organization = await _db.Organizations
            .Include(o => o.Members)
            .ThenInclude(m => m.User)
            .SingleEntityOrDefaultAsync(o => o.Id == orgId);
        using var orgScope = Log.BeginOrganizationScope(organization);

        if (organization is null)
        {
            return;
        }
        var platformUserIds = organization
            .Members
            .Select(m => m.User)
            .Where(u => u.NameIdentifier != Member.AbbotNameIdentifier)
            .Select(u => u.PlatformUserId)
            .ToList();

        if (platformUserIds.Count > 0)
        {
            var stopwatch = Stopwatch.StartNew();
            Log.UpdatingOrganizationMembers(platformUserIds.Count, organization.PlatformId, organization.Id);
            foreach (var platformUserId in platformUserIds)
            {
                await _slackResolver.ResolveMemberAsync(platformUserId, organization, forceRefresh: true);
            }
            Log.UpdatedOrganizationMembers(platformUserIds.Count, organization.PlatformId, organization.Id, stopwatch.Elapsed);
        }
    }
}

public static partial class UpdateUsersFromSlackApiJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Updating {MemberCount} members of Organization {PlatformId} (Id: {OrganizationId}) from Slack API.")]
    // ReSharper disable once InconsistentNaming
    public static partial void UpdatingOrganizationMembers(this ILogger<UpdateUsersFromSlackApiJob> logger, int memberCount, string platformId, int organizationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Updated {MemberCount} members of Organization {PlatformId} (Id: {OrganizationId}) from Slack API. Elapsed: {Elapsed}")]
    // ReSharper disable once InconsistentNaming
    public static partial void UpdatedOrganizationMembers(this ILogger<UpdateUsersFromSlackApiJob> logger, int memberCount, string platformId, int organizationId, TimeSpan elapsed);
}
