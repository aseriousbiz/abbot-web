using System.Collections.Generic;
using System.Linq;
using Hangfire;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;

namespace Serious.TestHelpers
{
    public class FakeBackgroundSlackClient : IBackgroundSlackClient
    {
        public bool EnqueueUpdateBotInformationCalled { get; private set; }

        public void EnqueueUpdateBotInformation(Organization organization)
        {
            EnqueueUpdateBotInformationCalled = true;
        }

        public bool EnqueueUpdateOrganizationCalled { get; private set; }

        public void EnqueueUpdateOrganization(Organization organization)
        {
            EnqueueUpdateOrganizationCalled = true;
        }

        public bool EnqueueMessageToInstallerCalled { get; set; }

        public void EnqueueMessageToInstaller(Organization organization, Member installer)
        {
            EnqueueMessageToInstallerCalled = true;
        }

        public bool EnqueueDirectMessagesCalled => EnqueueDirectMessagesCalls.Any();

        public List<DirectMessage> EnqueueDirectMessagesCalls { get; } = new List<DirectMessage>();

        public void EnqueueDirectMessages(Organization organization, IEnumerable<Member> members, string message) =>
            EnqueueDirectMessagesCalls.Add(new(
                organization.PlatformId,
                new(members.Select(m => m.User.PlatformUserId)),
                message));

        public bool EnqueueAdminWelcomeMessageCalled { get; private set; }

        public void EnqueueAdminWelcomeMessage(Organization organization, Member admin, Member actor)
        {
            EnqueueAdminWelcomeMessageCalled = true;
        }

        public bool EnqueueUpdateOrganizationScopesCalled { get; private set; }

        public IBackgroundJobClient BackgroundJobClient { get; set; } = new FakeBackgroundJobClient();

        public void EnqueueUpdateOrganizationScopes(Organization organization)
        {
            EnqueueUpdateOrganizationScopesCalled = true;
        }

        public string[] DMsSentTo(Organization organization, params Member[] members) =>
            EnqueueDirectMessagesCalls.Where(
                    call =>
                        call.TeamPlatformId == organization.PlatformId
                        && call.UserPlatformIds.SetEquals(
                            members.Select(m => m.User.PlatformUserId)))
                .Select(call => call.Message)
                .ToArray();

        public record DirectMessage(
            string TeamPlatformId,
            HashSet<string> UserPlatformIds,
            string Message);
    }
}
