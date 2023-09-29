using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Events;
using Serious.Abbot.PayloadHandlers;
using Xunit;

public class TeamChangePayloadHandlerTests
{
    public class TheOnPlatformEventAsyncMethod
    {
        [Fact]
        public async Task HandlesTeamRenameEvent()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            Assert.Null(organization.LastPlatformUpdate);
            var renameEvent = new TeamChangeEventPayload
            {
                TeamId = organization.PlatformId,
            };
            var platformEvent = env.CreateFakePlatformEvent(renameEvent);
            var handler = env.Activate<TeamChangePayloadHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.True(env.BackgroundSlackClient.EnqueueUpdateOrganizationCalled);
        }

        [Fact]
        public async Task HandlesTeamDomainChangeEvent()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            Assert.Null(organization.LastPlatformUpdate);
            var teamDomainChangeEvent = new TeamChangeEventPayload
            {
                TeamId = organization.PlatformId,
            };
            var platformEvent = env.CreateFakePlatformEvent(teamDomainChangeEvent);
            var handler = env.Activate<TeamChangePayloadHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            Assert.True(env.BackgroundSlackClient.EnqueueUpdateOrganizationCalled);
        }
    }
}
