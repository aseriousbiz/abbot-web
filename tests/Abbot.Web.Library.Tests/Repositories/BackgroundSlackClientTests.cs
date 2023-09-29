using System;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Clients;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Xunit;

public class BackgroundSlackClientTests
{
    public class TheUpdateOrganizationTeamInfoAsyncMethod
    {
        [Fact]
        public async Task UpdatesTeamNameOrganizationIdDomainAndAvatar()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            Assert.NotNull(organization.ApiToken);
            env.SlackApi.AddTeamInfo(organization.ApiToken.Reveal(), organization.PlatformId, new TeamInfo
            {
                Id = organization.PlatformId,
                Icon = new Icon
                {
                    Image68 = "https://app.ab.bot/avatar"
                },
                Name = "The Serious Org",
                Domain = "serious"
            });
            var client = env.Activate<BackgroundSlackClient>();

            await client.UpdateOrganizationTeamInfoAsync(organization.Id);

            var dbOrg = await env.Db.Organizations.FindAsync(organization.Id);
            Assert.NotNull(dbOrg);
            Assert.Equal("https://app.ab.bot/avatar", dbOrg.Avatar);
            Assert.Equal("The Serious Org", dbOrg.Name);
            Assert.Equal("serious.slack.com", dbOrg.Domain);
            Assert.Equal("serious", dbOrg.Slug);
        }

        [Fact]
        public async Task DoesNotUpdateAvatarIfBadResponse()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync(avatar: "https://localhost/coolavatar");
            Assert.NotNull(organization.ApiToken);
            env.SlackApi.AddTeamInfoResponse(organization.ApiToken.Reveal(), organization.PlatformId, new TeamInfoResponse
            {
                Ok = false,
                Error = "Bad request"
            });
            var client = env.Activate<BackgroundSlackClient>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.UpdateOrganizationTeamInfoAsync(organization.Id));

            var dbOrg = await env.Db.Organizations.FindAsync(organization.Id);
            Assert.NotNull(dbOrg);
            Assert.Equal("https://localhost/coolavatar", dbOrg.Avatar);
        }
    }

    public class TheSendMessageToInstallerAsyncMethod
    {
        [Fact]
        public async Task SendsWelcomeMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var admin = await env.CreateAdminMemberAsync();
            var client = env.Activate<BackgroundSlackClient>();

            await client.SendMessageToInstallerAsync(organization.Id, admin.User.PlatformUserId);

            var posted = Assert.Single(env.SlackApi.PostedMessages);
            Assert.StartsWith(":wave: Hi there! Thanks for installing Abbot!", posted.Text);
            Assert.NotNull(posted.Blocks);
            var section = Assert.IsType<Section>(posted.Blocks.First());
            Assert.NotNull(section.Text);
            var actions = Assert.IsType<Actions>(posted.Blocks.FindBlockById("i:AdminWelcomeHandler"));
            Assert.Collection(actions.Elements,
                b => AssertButton(b, ":key: Manage Administrators", "admins"),
                b => AssertButton(b, ":envelope: Invite Users", "invite", new Uri("https://app.ab.bot/settings/organization/users/invite")),
                b => AssertButton(b, ":gear: Customize Abbot", "customize", new Uri("https://app.ab.bot/settings/organization")),
                b => AssertButton(b, ":hourglass: Remind me tomorrow", "remind"));
        }
    }

    public class TheSendAdminWelcomeMessageAsyncMethod
    {
        [Fact]
        public async Task SendsWelcomeMessage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = await env.CreateAdminMemberAsync();
            var admin = await env.CreateAdminMemberAsync();
            var client = env.Activate<BackgroundSlackClient>();

            await client.SendAdminWelcomeMessageAsync(
                organization.Id,
                admin.User.PlatformUserId,
                actor.User.PlatformUserId);

            var posted = Assert.Single(env.SlackApi.PostedMessages);
            Assert.Equal(
                $"{actor.User.ToMention()} added you to the Administrators role for your workspace’s Abbot. You can customize how I fit in with your organization and configure how I respond when tracking conversations.",
                posted.Text);

            Assert.NotNull(posted.Blocks);
            var section = Assert.IsType<Section>(posted.Blocks.First());
            Assert.NotNull(section.Text);
            Assert.Equal(posted.Text, section.Text.Text);
            var actions = Assert.IsType<Actions>(posted.Blocks.Last());
            var welcomeBlock = posted.Blocks.FindBlockById("i:AdminWelcomeHandler");
            Assert.NotNull(welcomeBlock);
            Assert.Collection(actions.Elements,
                b => AssertButton(b, ":key: Manage Administrators", "admins"),
                b => AssertButton(b, ":envelope: Invite Users", "invite", new Uri("https://app.ab.bot/settings/organization/users/invite")),
                b => AssertButton(b,
                    ":gear: Customize Abbot",
                    "customize",
                    new Uri("https://app.ab.bot/settings/organization")),
                b => AssertButton(b, ":hourglass: Remind me tomorrow", "remind"));
        }
    }

    static void AssertButton(IActionElement element, string text, string value, Uri? url = null)
    {
        var button = Assert.IsType<ButtonElement>(element);
        Assert.Equal(text, button.Text.Text);
        Assert.Equal(value, button.Value);
        Assert.Equal(url, button.Url);
    }
}
