using System;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Ends the trials for all organizations with expired trials. This is scheduled in <see cref="RecurringJobsSeeder"/>.
/// </summary>
public class ExpireTrialsJob : IRecurringJob
{
    const int DaysBeforeExpirationWhenWeSendAWarning = 7;

    readonly IOrganizationRepository _organizationRepository;
    readonly IRoleManager _roleManager;
    readonly ISlackApiClient _slackApiClient;
    readonly IClock _clock;

    public ExpireTrialsJob(
        IOrganizationRepository organizationRepository,
        IRoleManager roleManager,
        ISlackApiClient slackApiClient,
        IClock clock)
    {
        _organizationRepository = organizationRepository;
        _roleManager = roleManager;
        _slackApiClient = slackApiClient;
        _clock = clock;
    }

    // For calling in the job.
    public static string Name => "Expire Trials";

    [Queue(HangfireQueueNames.Maintenance)]
    public Task RunAsync(CancellationToken cancellationToken = default) => ExpireTrialsAsync(_clock.UtcNow, cancellationToken);

    // For calling in tests, or other places.
    public async Task ExpireTrialsAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var expiringOrganizations = await _organizationRepository
            .GetExpiringTrialsAsync(nowUtc, DaysBeforeExpirationWhenWeSendAWarning);
        foreach (var organization in expiringOrganizations)
        {
            await SendExpiringNoticeAsync(organization, DaysBeforeExpirationWhenWeSendAWarning);
        }

        // Get all organizations with trials that have expired.
        var expiredOrganizations = await _organizationRepository.GetExpiredTrialsAsync(nowUtc);

        foreach (var organization in expiredOrganizations)
        {
            await _organizationRepository.EndTrialAsync(organization, "Trial period expired.", null);
            await SendExpirationNoticeAsync(organization);
        }
    }

    async Task SendExpiringNoticeAsync(Organization organization, int days)
    {
        string fallbackText = $"👋 Hi there! Your Abbot trial will expire in {days.ToQuantity("day")}.";
        await NotifyAdminsAsync(
            organization,
            fallbackText);
    }

    async Task SendExpirationNoticeAsync(Organization organization)
    {
        string fallbackText = "👋 Hi there! Your Abbot trial has expired / subscription has ended.";
        await NotifyAdminsAsync(
            organization,
            fallbackText);
    }

    async Task NotifyAdminsAsync(Organization organization, string fallbackText)
    {
        var apiToken = organization.ApiToken?.Reveal();
        if (apiToken is not null)
        {
            var fullText = $"{fallbackText} Please sign up for a paid account at https://ab.bot if you’d like me to continue monitoring conversations. Contact us at <mailto:help@ab.bot|help@ab.bot> if you need help or have any questions.";
            var admins = await _roleManager.GetMembersInRoleAsync(Roles.Administrator, organization);
            foreach (var admin in admins)
            {
                await _slackApiClient.SendDirectMessageAsync(
                    organization,
                    admin.User,
                    fallbackText,
                    new Section(new MrkdwnText(fullText)));
            }
        }
    }
}
