using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Xunit;

public class BotMembershipJobTests
{
    public class TheRunAsyncMethod
    {
        [Fact]
        public async Task UpdatesJobBasedOnSlackApi()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IOrganizationApiSyncer>(out var syncer)
                .Build();
            var organization = env.TestData.Organization;
            organization.PlanType = PlanType.Business;
            await env.Db.SaveChangesAsync();
            var job = env.Activate<BotMembershipJob>();

            await job.RunAsync();

            await syncer.Received().UpdateRoomsFromApiAsync(organization);
        }

        [Fact]
        public async Task IgnoresNonBusinessPlanOrganizations()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IOrganizationApiSyncer>(out var syncer)
                .Build();
            var organization = env.TestData.Organization;
            var job = env.Activate<BotMembershipJob>();

            await job.RunAsync();

            await syncer.DidNotReceive().UpdateRoomsFromApiAsync(Args.Organization);
        }
    }
}
