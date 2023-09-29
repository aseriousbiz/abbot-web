using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Job to query for organizations that haven't been updated in a while and update the information we have
/// for them from the API.
/// </summary>
public class UpdateOrganizationsFromSlackApiJob : IRecurringJob
{
    static readonly ILogger<UpdateOrganizationsFromSlackApiJob> Log = ApplicationLoggerFactory.CreateLogger<UpdateOrganizationsFromSlackApiJob>();

    const int DaysBeforeUpdate = 1;

    readonly IOrganizationRepository _organizationRepository;
    readonly ISlackApiClient _slackApiClient;

    public UpdateOrganizationsFromSlackApiJob(
        IOrganizationRepository organizationRepository,
         ISlackApiClient slackApiClient)
    {
        _organizationRepository = organizationRepository;
        _slackApiClient = slackApiClient;
    }

    public static string Name => "Update Slack Organizations";

    [Queue(HangfireQueueNames.Maintenance)]
    [AutomaticRetry(Attempts = 0)] // We don't want this job to retry. It'll run again on its next scheduled time.
    public async Task RunAsync(CancellationToken cancellationToken = default)
        => await UpdateOrganizationsAsync(DaysBeforeUpdate, cancellationToken);

    public async Task UpdateOrganizationsAsync(int daysBeforeUpdate, CancellationToken cancellationToken = default)
    {
        // Update up to 100 organizations at a time.
        var organizations = await _organizationRepository
            .GetOrganizationsToUpdateFromApiAsync(daysBeforeUpdate);

        if (organizations.Count > 0)
        {
            Log.UpdatingOrganizations(organizations.Count);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                foreach (var organization in organizations)
                {
                    var apiToken = organization.RequireAndRevealApiToken();

                    var response = await _slackApiClient.GetTeamInfoAsync(apiToken, organization.PlatformId);
                    if (response.IsSuccessStatusCode && response.Content.Ok)
                    {
                        var teamInfo = response.Content.Body;

                        await _organizationRepository.UpdateOrganizationAsync(organization, teamInfo);
                    }
                }

                Log.UpdatedOrganizations(organizations.Count, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }
        }
    }
}

public static partial class UpdateOrganizationsFromSlackApiJobLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Updating {OrganizationCount} organizations from Slack API.")]
    // ReSharper disable once InconsistentNaming
    public static partial void UpdatingOrganizations(this ILogger<UpdateOrganizationsFromSlackApiJob> logger, int organizationCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Updated {OrganizationCount} organizations from Slack API. Elapsed: {Elapsed}")]
    // ReSharper disable once InconsistentNaming
    public static partial void UpdatedOrganizations(this ILogger<UpdateOrganizationsFromSlackApiJob> logger, int organizationCount, TimeSpan elapsed);
}
