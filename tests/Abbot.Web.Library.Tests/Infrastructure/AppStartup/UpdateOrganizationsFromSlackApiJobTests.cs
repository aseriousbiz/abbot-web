using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Slack;
using Xunit;

public class UpdateOrganizationsFromSlackApiJobTests
{
    public class TheUpdateOrganizationsAsyncMethod
    {
        [Fact]
        public async Task UpdatesOrganizationsFromApi()
        {
            var env = TestEnvironment.Create();
            var org1 = env.TestData.Organization;
            org1.Slug = "test-org-1";
            org1.LastPlatformUpdate = env.Clock.UtcNow.AddDays(-15);
            var org2 = await env.CreateOrganizationAsync();
            org2.Slug = "test-org-2";
            org2.LastPlatformUpdate = env.Clock.UtcNow.AddDays(-15);
            var org3 = await env.CreateOrganizationAsync();
            var org3OriginalName = org3.Name;
            org3.LastPlatformUpdate = env.Clock.UtcNow.AddDays(-13);
            org3.Slug = "test-org-3";
            await env.Db.SaveChangesAsync();
            env.Clock.Freeze();
            env.SlackApi.AddTeamInfo(
                org1.ApiToken!.Reveal(),
                org1.PlatformId,
                new TeamInfo
                {
                    Id = org1.PlatformId,
                    Name = "New Team 1",
                    Domain = "new-team-1",
                    Icon = new()
                    {
                        Image68 = "https://example.com/new-team-1-icon.png"
                    }
                });
            env.SlackApi.AddTeamInfo(
                org2.ApiToken!.Reveal(),
                org2.PlatformId,
                new TeamInfo
                {
                    Id = org2.PlatformId,
                    Name = "New Team 2",
                    Domain = "new-team-2",
                    Icon = new()
                    {
                        Image68 = "https://example.com/new-team-2-icon.png"
                    }
                });
            env.SlackApi.AddTeamInfo(
                org3.ApiToken!.Reveal(),
                org3.PlatformId,
                new TeamInfo
                {
                    Id = org3.PlatformId,
                    Name = "New Team 3",
                    Icon = new()
                    {
                        Image68 = "https://example.com/new-team-3-icon.png"
                    }
                });
            var job = env.Activate<UpdateOrganizationsFromSlackApiJob>();

            await job.UpdateOrganizationsAsync(14);

            Assert.Equal("New Team 1", org1.Name);
            Assert.Equal("new-team-1", org1.Slug);
            Assert.Equal("new-team-1.slack.com", org1.Domain);
            Assert.Equal("https://example.com/new-team-1-icon.png", org1.Avatar);
            Assert.Equal("New Team 2", org2.Name);
            Assert.Equal("new-team-2", org2.Slug);
            Assert.Equal("new-team-2.slack.com", org2.Domain);
            Assert.Equal("https://example.com/new-team-2-icon.png", org2.Avatar);
            Assert.Equal(org3OriginalName, org3.Name);
        }
    }
}
