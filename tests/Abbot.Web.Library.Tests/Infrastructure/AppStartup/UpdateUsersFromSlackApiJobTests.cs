using Abbot.Common.TestHelpers;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Slack;

public class UpdateUsersFromSlackApiJobTests
{
    public class TheUpdateUsersAsyncMethod
    {
        [Fact]
        public async Task UpdatesUsersFromApi()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            Assert.False(member.IsGuest);
            env.SlackApi.AddUserInfoResponse(organization.ApiToken!.Reveal(), new UserInfo
            {
                Id = member.User.PlatformUserId,
                Profile = new UserProfile { DisplayName = "the-user" },
                IsRestricted = true,
                TeamId = organization.PlatformId,
            });
            var job = env.Activate<UpdateUsersFromSlackApiJob>();

            await job.UpdateUsersAsync(organization.Id);

            Assert.Equal("the-user", member.DisplayName);
            Assert.True(member.IsGuest);
        }
    }
}
