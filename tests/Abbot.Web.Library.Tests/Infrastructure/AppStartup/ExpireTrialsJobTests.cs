using System;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Models;
using Serious.Slack.BlockKit;
using Xunit;

public class ExpireTrialsJobTests
{
    [Fact]
    public async Task EndsTrialsThatHaveExpired()
    {
        var now = new DateTime(2022, 4, 26, 0, 0, 0, DateTimeKind.Utc);

        var env = TestEnvironment.Create();
        var orgWithExpiredTrialInPast = await env.CreateOrganizationAsync();
        var admin = await env.CreateAdminMemberAsync(org: orgWithExpiredTrialInPast);
        orgWithExpiredTrialInPast.Trial = new(PlanType.Beta, now.AddSeconds(-1));
        var orgWithExpiredTrialRightNow = await env.CreateOrganizationAsync();
        orgWithExpiredTrialRightNow.Trial = new(PlanType.Beta, now);
        var orgWithExpiredTrialInFuture = await env.CreateOrganizationAsync();
        orgWithExpiredTrialInFuture.Trial = new(PlanType.Beta, now.AddSeconds(1));
        await env.Db.SaveChangesAsync();

        var job = env.Activate<ExpireTrialsJob>();
        await job.ExpireTrialsAsync(now);

        await env.ReloadAsync(orgWithExpiredTrialInFuture, orgWithExpiredTrialRightNow, orgWithExpiredTrialInPast);
        Assert.Equal(PlanType.Beta, orgWithExpiredTrialInFuture.Trial?.Plan);
        Assert.Null(orgWithExpiredTrialInPast.Trial);
        Assert.Null(orgWithExpiredTrialRightNow.Trial);
        var expiredMessage = Assert.Single(env.SlackApi.PostedMessages);
        Assert.Equal("👋 Hi there! Your Abbot trial has expired / subscription has ended.", expiredMessage.Text);
        Assert.NotNull(expiredMessage.Blocks);
        var section = Assert.IsType<Section>(Assert.Single(expiredMessage.Blocks));
        Assert.Equal("👋 Hi there! Your Abbot trial has expired / subscription has ended. Please sign up for a paid account at https://ab.bot if you’d like me to continue monitoring conversations. Contact us at <mailto:help@ab.bot|help@ab.bot> if you need help or have any questions.", section.Text?.Text);
        Assert.Equal(admin.User.PlatformUserId, expiredMessage.Channel);
    }

    [Fact]
    public async Task SendsExpiringMessageSevenDaysBeforeExpiration()
    {
        var now = new DateTime(2022, 4, 26, 0, 0, 0, DateTimeKind.Utc);

        var env = TestEnvironment.Create();
        var orgExpiringInSevenDays = await env.CreateOrganizationAsync();
        var admin = await env.CreateAdminMemberAsync(org: orgExpiringInSevenDays);
        orgExpiringInSevenDays.Trial = new(PlanType.Beta, now.AddDays(7));
        var orgWithExpiredTrialRightNow = await env.CreateOrganizationAsync();
        orgWithExpiredTrialRightNow.Trial = new(PlanType.Beta, now);
        var orgWithExpiredTrialInFuture = await env.CreateOrganizationAsync();
        orgWithExpiredTrialInFuture.Trial = new(PlanType.Beta, now.AddSeconds(1));
        await env.Db.SaveChangesAsync();

        var job = env.Activate<ExpireTrialsJob>();
        await job.ExpireTrialsAsync(now);

        await env.ReloadAsync(orgWithExpiredTrialInFuture, orgWithExpiredTrialRightNow, orgExpiringInSevenDays);
        var expiredMessage = Assert.Single(env.SlackApi.PostedMessages);
        Assert.Equal("👋 Hi there! Your Abbot trial will expire in 7 days.", expiredMessage.Text);
        Assert.NotNull(expiredMessage.Blocks);
        var section = Assert.IsType<Section>(Assert.Single(expiredMessage.Blocks));
        Assert.Equal("👋 Hi there! Your Abbot trial will expire in 7 days. Please sign up for a paid account at https://ab.bot if you’d like me to continue monitoring conversations. Contact us at <mailto:help@ab.bot|help@ab.bot> if you need help or have any questions.", section.Text?.Text);
        Assert.Equal(admin.User.PlatformUserId, expiredMessage.Channel);
    }
}
