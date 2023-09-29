using Abbot.Common.TestHelpers;
using Argon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Models;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.Slack;
using Serious.TestHelpers;

public class OrganizationRepositoryTests
{
    public class TheGetAsyncMethod
    {
        [Fact]
        public async Task ReturnsExistingOrganization()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            var repository = env.Activate<OrganizationRepository>();

            var result = await repository.GetAsync(organization.PlatformId);

            Assert.NotNull(result);
            Assert.Equal(organization.PlatformId, result.PlatformId);
            Assert.Equal(organization.Domain, result.Domain);
        }
    }

    public class TheEnsureAsyncMethod
    {
        [Fact]
        public async Task CreatesNewOrganization()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var principal = new FakeClaimsPrincipal(
                "T0123456789",
                user.PlatformUserId,
                "Some cool team",
                "cool.slack.com");

            var repository = env.Activate<OrganizationRepository>();

            var (organization, isNew) = await repository.EnsureAsync(principal);

            Assert.True(isNew);
            Assert.Equal("T0123456789", organization.PlatformId);
            Assert.Null(organization.EnterpriseGridId);
            Assert.Equal("Some cool team", organization.Name);
            Assert.Equal("cool.slack.com", organization.Domain);
            Assert.Equal("cool", organization.Slug);
        }

        [Fact]
        public async Task CreatesNewEnterpriseOrganization()
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var principal = new FakeClaimsPrincipal(
                "T0123456789",
                user.PlatformUserId,
                "Some cool team",
                "cool.slack.com",
                enterpriseId: "E0123456789");

            var repository = env.Activate<OrganizationRepository>();

            var (organization, isNew) = await repository.EnsureAsync(principal);

            Assert.True(isNew);
            Assert.Equal("T0123456789", organization.PlatformId);
            Assert.Equal("E0123456789", organization.EnterpriseGridId);
            Assert.Equal(PlanType.None, organization.PlanType);
            Assert.Equal("Some cool team", organization.Name);
            Assert.Equal("cool.slack.com", organization.Domain);
            Assert.Equal("cool", organization.Slug);
        }

        [Fact]
        public async Task ReturnsExistingOrganization()
        {
            var env = TestEnvironment.CreateWithoutData();
            var existing = await env.CreateOrganizationAsync("T0123456789");
            var principal = new FakeClaimsPrincipal(
                "T0123456789",
                "U000001101",
                "Some cool team",
                "cool.example.com",
                enterpriseId: "E000000001",
                enterpriseName: "Cool Enterprise",
                enterpriseDomain: "e0123441");

            var repository = env.Activate<OrganizationRepository>();

            var (organization, isNew) = await repository.EnsureAsync(principal);

            Assert.False(isNew);
            Assert.Equal("T0123456789", organization.PlatformId);
            Assert.Equal(organization.Id, existing.Id);
            Assert.Equal("E000000001", organization.EnterpriseGridId);
        }
    }

    public class TheCreateOrganizationAsyncMethod
    {
        [Fact]
        public async Task CreatesNewOrganization()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<OrganizationRepository>();

            var organization = await repository.CreateOrganizationAsync(
                platformId: "T0123456789",
                PlanType.Free,
                name: "Test",
                domain: "test.slack.com",
                slug: "test",
                avatar: null);

            Assert.Equal("T0123456789", organization.PlatformId);
            var abbot = await env.Db.Members.Where(m => m.OrganizationId == organization.Id).SingleAsync();
            Assert.True(abbot.IsAbbot());
        }

        [Fact] // A race condition could cause this.
        public async Task ReturnsExistingOrganizationForSamePlatformId()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<OrganizationRepository>();
            var organization = env.TestData.Organization;
            env.Db.ThrowUniqueConstraintViolationOnSave("Organizations", "IX_Organizations_PlatformId");

            var existing = await repository.CreateOrganizationAsync(
                platformId: organization.PlatformId,
                PlanType.Free,
                name: "Test",
                domain: "test.slack.com",
                slug: "test",
                avatar: null);

            Assert.Equal(existing.Id, organization.Id);
        }
    }

    public class TheDeleteOrganizationAsyncMethod
    {
        [Fact]
        public async Task DeletesTheOrganizationAndChildRecords()
        {
            var env = TestEnvironment.Create();
            var staffMember = await env.CreateStaffMemberAsync();
            var repository = env.Activate<OrganizationRepository>();
            var deletedOrganization = await repository.CreateOrganizationAsync(
                platformId: "T0123456789",
                PlanType.Unlimited,
                name: "Test",
                domain: "test.slack.com",
                slug: "test",
                avatar: null);

            var anotherOrganization = await repository.CreateOrganizationAsync(
                platformId: "T08675309",
                PlanType.Unlimited,
                name: "Foo",
                domain: "foo.slack.com",
                slug: "foo",
                avatar: null);

            var deletedRoom = await env.CreateRoomAsync(org: deletedOrganization);
            var deletedMember = await env.CreateMemberAsync(org: deletedOrganization);

            var deletedSkill = await env.CreateSkillAsync("goodbye", org: deletedOrganization);
            var deletedPackage =
                await env.Packages.CreateAsync(new PackageCreateModel(), deletedSkill, deletedMember.User);

            var skill = await env.CreateSkillAsync("hello", org: anotherOrganization);
            skill.SourcePackageVersionId = deletedPackage.Versions.Single().Id;
            await env.Db.SaveChangesAsync();
            var anotherMember = await env.CreateMemberAsync(org: anotherOrganization);
            var anotherRoom = await env.CreateRoomAsync(org: anotherOrganization);
            var conversation = await env.CreateConversationAsync(anotherRoom);
            var conversationLink = await env.CreateConversationLinkAsync(
                conversation,
                ConversationLinkType.ZendeskTicket,
                "xyz",
                null,
                deletedMember);

            var messagePostedEvent = new MessagePostedEvent
            {
                Member = deletedMember,
                Created = env.Clock.UtcNow,
            };

            conversation.Events.Add(messagePostedEvent);
            await env.Db.SaveChangesAsync();
            // Just need some audit events to make sure the right ones get deleted.
            // But I need audit events where ActorMember is set for testing. Right now, AdminAuditEvents don't set it.
            await env.AuditLog.LogRoomLinkedAsync(deletedRoom,
                RoomLinkType.ZendeskOrganization,
                "xyz",
                "xyz",
                deletedMember,
                deletedOrganization); // This one should go away.

            await env.AuditLog.LogRoomLinkedAsync(anotherRoom,
                RoomLinkType.ZendeskOrganization,
                "xyz",
                "xyz",
                deletedMember,
                anotherOrganization); // This is not a real scenario, but want to test that we deleted it because actor is in deleted org.

            await env.AuditLog.LogRoomLinkedAsync(anotherRoom,
                RoomLinkType.ZendeskOrganization,
                "xyz",
                "xyz",
                anotherMember,
                deletedOrganization); // This is not a real scenario, but want to test that we deleted it because org is deleted.

            await env.AuditLog.LogRoomLinkedAsync(anotherRoom,
                RoomLinkType.ZendeskOrganization,
                "xyz",
                "xyz",
                anotherMember,
                anotherOrganization); // Should stay around

            Assert.True(await env.Db.AuditEvents.Include(e => e.ActorMember).OfType<RoomLinkedEvent>()
                .Where(e => e.ActorMemberId == deletedMember.Id).AnyAsync());

            Assert.True(await env.Db.AuditEvents.Include(e => e.ActorMember).OfType<RoomLinkedEvent>()
                .Where(e => e.ActorMemberId == anotherMember.Id).AnyAsync());

            await repository.DeleteOrganizationAsync(deletedOrganization.PlatformId, "Unit test", staffMember);

            var deletedAuditEvent =
                await env.Db.AuditEvents.SingleOrDefaultAsync(e => e.OrganizationId == staffMember.OrganizationId);

            Assert.NotNull(deletedAuditEvent);
            Assert.Equal("Deleted Test (T0123456789) organization.", deletedAuditEvent.Description);
            // Make sure we deleted the right organizations and audit events, and not the wrong ones.
            Assert.Null(
                await env.Db.Organizations.SingleOrDefaultAsync(o => o.PlatformId == deletedOrganization.PlatformId));

            Assert.Equal(anotherOrganization,
                await env.Db.Organizations.SingleOrDefaultAsync(o => o.PlatformId == anotherOrganization.PlatformId));

            Assert.False(await env.Db.AuditEvents.Where(e => e.OrganizationId == deletedOrganization.Id).AnyAsync());
            Assert.True(await env.Db.AuditEvents.Where(e => e.OrganizationId == anotherOrganization.Id).AnyAsync());
            Assert.Null(await env.Db.Rooms.SingleOrDefaultAsync(r => r.Id == deletedRoom.Id));
            Assert.NotNull(await env.Db.Rooms.SingleOrDefaultAsync(r => r.Id == anotherRoom.Id));
            Assert.False(await env.Db.AuditEvents.Include(e => e.ActorMember).OfType<RoomLinkedEvent>()
                .Where(e => e.ActorMemberId == deletedMember.Id).AnyAsync());

            Assert.True(await env.Db.AuditEvents.Include(e => e.ActorMember).OfType<RoomLinkedEvent>()
                .Where(e => e.ActorMemberId == anotherMember.Id).AnyAsync());

            Assert.Null(await env.Db.Skills.SingleOrDefaultAsync(s => s.Id == deletedSkill.Id));
            // Make sure we delete the source skill, but not the installed skill.
            var installedSkill = await env.Db.Skills.SingleOrDefaultAsync(s => s.Id == skill.Id);
            Assert.NotNull(installedSkill);
            Assert.Null(skill.SourcePackageVersionId);
            Assert.Null(await env.Db.ConversationLinks.SingleOrDefaultAsync(l => l.Id == conversationLink.Id));
            Assert.Null(await env.Db.ConversationEvents.SingleOrDefaultAsync(e => e.Id == messagePostedEvent.Id));
            Assert.NotNull(await env.Db.Conversations.SingleOrDefaultAsync(c => c.Id == conversation.Id));
        }
    }

    public class TheUpdateOrganizationAsyncMethod
    {
        [Fact]
        public async Task UpdatesOrganizationBasedOnLoggedInUser()
        {
            var env = TestEnvironment.Create();

            // We're using CreateOrganizationAsync on the IOrganizationRepository directly to bypass
            // the automated name/domain/id generation.
            var organization = await env.Organizations.CreateOrganizationAsync(
                platformId: "T99990000",
                plan: PlanType.None,
                name: null,
                domain: null,
                slug: "nonexistia",
                avatar: null);

            await env.Db.SaveChangesAsync();
            var user = env.TestData.User;
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                "Some cool team",
                "cool.example.com");

            var repository = env.Activate<OrganizationRepository>();

            await repository.UpdateOrganizationAsync(organization, principal);

            Assert.Equal("Some cool team", organization.Name);
            Assert.Equal("cool.example.com", organization.Domain);
            Assert.Equal(PlanType.Free, organization.PlanType);
        }

        [Fact]
        public async Task DoesNotUpdateCompleteOrganizations()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var originalName = organization.Name;
            var originalDomain = organization.Domain;
            var principal = new FakeClaimsPrincipal(
                organization.PlatformId,
                user.PlatformUserId,
                "Some cool team",
                "cool.example.com");

            var repository = env.Activate<OrganizationRepository>();

            await repository.UpdateOrganizationAsync(organization, principal);

            Assert.Equal(originalName, organization.Name);
            Assert.Equal(originalDomain, organization.Domain);
            Assert.Equal(PlanType.Unlimited, organization.PlanType);
            Assert.False(env.BackgroundSlackClient.EnqueueUpdateOrganizationCalled);
        }

        [Fact]
        public async Task WithTeamInfoUpdatesOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamInfo = new TeamInfo
            {
                Id = organization.PlatformId,
                Name = "Some cool team",
                Domain = "cool",
                Icon = new Icon
                {
                    Image68 = "https://example.com/icon.png"
                }
            };

            var repository = env.Activate<OrganizationRepository>();

            await repository.UpdateOrganizationAsync(organization, teamInfo);

            Assert.Equal("Some cool team", organization.Name);
            Assert.Equal("cool.slack.com", organization.Domain);
            Assert.Equal("cool", organization.Slug);
            Assert.Equal("https://example.com/icon.png", organization.Avatar);
            Assert.Equal(string.Empty, organization.EnterpriseGridId);
        }

        [Fact]
        public async Task WithTeamInfoUpdatesForeignEnterpriseOrganization()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync("E0123456789");
            var teamInfo = new TeamInfo
            {
                Id = organization.PlatformId,
                Name = "Some cool team",
                Domain = "cool",
                Icon = new Icon
                {
                    Image68 = "https://example.com/icon.png"
                }
            };

            var repository = env.Activate<OrganizationRepository>();

            await repository.UpdateOrganizationAsync(organization, teamInfo);

            Assert.Equal("Some cool team", organization.Name);
            Assert.Equal("cool.slack.com", organization.Domain);
            Assert.Equal("cool", organization.Slug);
            Assert.Equal("https://example.com/icon.png", organization.Avatar);
            Assert.Equal("E0123456789", organization.EnterpriseGridId);
        }

        [Fact]
        public async Task WithTeamInfoUpdatesEnterpriseOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamInfo = new TeamInfo
            {
                Id = organization.PlatformId,
                Name = "Some cool team",
                Domain = "cool",
                EnterpriseId = "E0123456789",
                Icon = new Icon
                {
                    Image68 = "https://example.com/icon.png"
                }
            };

            var repository = env.Activate<OrganizationRepository>();

            await repository.UpdateOrganizationAsync(organization, teamInfo);

            Assert.Equal("Some cool team", organization.Name);
            Assert.Equal("cool.slack.com", organization.Domain);
            Assert.Equal("cool", organization.Slug);
            Assert.Equal("https://example.com/icon.png", organization.Avatar);
            Assert.Equal("E0123456789", organization.EnterpriseGridId);
        }
    }

    public class TheInstallBotAsyncMethod
    {
        [Theory]
        [InlineData(null, PlanType.Free, true)]
        [InlineData(PlanType.Free, PlanType.Free, true)]
        [InlineData(PlanType.Unlimited, PlanType.Unlimited, false)]
        public async Task CreatesOrganization(PlanType? defaultPlan, PlanType expectedPlan, bool trialEligible)
        {
            var env = TestEnvironmentBuilder.CreateWithoutData()
                .ConfigureServices(s => {
                    if (defaultPlan is not null)
                    {
                        s.Configure<AbbotOptions>(o => o.DefaultPlan = defaultPlan.Value);
                    }
                }).Build();
            var installEvent = new InstallEvent(
                PlatformId: "T001Slack123",
                PlatformType.Slack,
                BotId: "B001",
                BotUserId: "U001",
                BotName: "abbot-bot",
                BotAppName: "AbbotApp",
                BotAvatar: "https://example.com/bot-avatar.png",
                Avatar: "https://example.com/org-avatar.png",
                Domain: "the-domain.slack.com",
                Name: "The A Team",
                Slug: "the-a-team",
                ApiToken: new SecretString("new api token", new FakeDataProtectionProvider()),
                EnterpriseId: null,
                OAuthScopes: "new-scopes",
                AppId: "some-app-id"
            );

            var repository = env.Activate<OrganizationRepository>();

            var organization = await repository.InstallBotAsync(installEvent);

            Assert.Equal("T001Slack123", organization.PlatformId);
            Assert.Equal("B001", organization.PlatformBotId);
            Assert.Equal("U001", organization.PlatformBotUserId);
            Assert.Equal("https://example.com/org-avatar.png", organization.Avatar);
            Assert.Equal("https://example.com/bot-avatar.png", organization.BotAvatar);
            Assert.Equal("The A Team", organization.Name);
            Assert.Equal("the-domain.slack.com", organization.Domain);
            Assert.Equal("the-a-team", organization.Slug);
            Assert.Equal("abbot-bot", organization.BotName);
            Assert.Equal("some-app-id", organization.BotAppId);
            Assert.Equal("AbbotApp", organization.BotAppName);
            Assert.Equal(expectedPlan, organization.PlanType);
            Assert.Equal(trialEligible, organization.TrialEligible);
            Assert.NotNull(organization.ApiToken);
            Assert.Equal("new api token", organization.ApiToken.Reveal());
            Assert.Equal("new-scopes", organization.Scopes);
            var users = await env.Db.Users.ToListAsync();
            var abbot = Assert.Single(users); // Only the system bot user should be there.
            Assert.True(abbot.IsAbbot);
            Assert.True(abbot.IsBot);
        }

        [Fact]
        public async Task CreatesEnterpriseOrganization()
        {
            var env = TestEnvironment.CreateWithoutData();
            var installEvent = new InstallEvent(
                PlatformId: "T001Slack123",
                PlatformType.Slack,
                BotId: "B001",
                BotUserId: "U001",
                BotName: "abbot-bot",
                BotAppName: "AbbotApp",
                BotAvatar: "https://example.com/bot-avatar.png",
                Avatar: "https://example.com/org-avatar.png",
                Domain: "the-domain.slack.com",
                Name: "The A Team",
                Slug: "the-a-team",
                ApiToken: new SecretString("new api token", new FakeDataProtectionProvider()),
                EnterpriseId: "E0001234132",
                OAuthScopes: "new-scopes",
                AppId: "some-app-id"
            );

            var repository = env.Activate<OrganizationRepository>();

            var organization = await repository.InstallBotAsync(installEvent);

            Assert.Equal("T001Slack123", organization.PlatformId);
            Assert.Equal("B001", organization.PlatformBotId);
            Assert.Equal("U001", organization.PlatformBotUserId);
            Assert.Equal("https://example.com/org-avatar.png", organization.Avatar);
            Assert.Equal("https://example.com/bot-avatar.png", organization.BotAvatar);
            Assert.Equal("The A Team", organization.Name);
            Assert.Equal("the-domain.slack.com", organization.Domain);
            Assert.Equal("the-a-team", organization.Slug);
            Assert.Equal("abbot-bot", organization.BotName);
            Assert.Equal("some-app-id", organization.BotAppId);
            Assert.Equal("AbbotApp", organization.BotAppName);
            Assert.Equal(PlanType.Free, organization.PlanType);
            Assert.NotNull(organization.ApiToken);
            Assert.Equal("new api token", organization.ApiToken.Reveal());
            Assert.Equal("new-scopes", organization.Scopes);
            Assert.Equal("E0001234132", organization.EnterpriseGridId);
            var users = await env.Db.Users.ToListAsync();
            var abbot = Assert.Single(users); // Only the system bot user should be there.
            Assert.True(abbot.IsAbbot);
            Assert.True(abbot.IsBot);
        }

        [Fact]
        public async Task UpdatesExistingOrganizationInfoSuchAsApiToken()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync(
                slug: "slack123",
                platformId: "T001Slack123",
                domain: "old-domain",
                name: "slack",
                apiToken: "old api token",
                scopes: "old scopes");

            organization.BotAppName = "Old Abbot";
            organization.PlatformBotId = null; // This is only updated if it's null.
            await env.Db.SaveChangesAsync();
            var installEvent = new InstallEvent(
                "T001Slack123",
                PlatformType.Slack,
                BotId: "B001",
                BotName: "abbot",
                "Org Name",
                "org-slug",
                ApiToken: new SecretString("new api token", new FakeDataProtectionProvider()),
                EnterpriseId: null,
                Domain: "new-domain",
                OAuthScopes: "new-scopes",
                BotAppName: "New Abbot",
                BotUserId: "U001",
                BotAvatar: "https://botavatar",
                Avatar: "https://orgavatar"
            );

            var repository = env.Activate<OrganizationRepository>();

            await repository.InstallBotAsync(installEvent);

            await env.ReloadAsync(organization);
            Assert.Equal("B001", organization.PlatformBotId);
            Assert.Equal("U001", organization.PlatformBotUserId);
            Assert.NotNull(organization.ApiToken);
            Assert.Equal("new api token", organization.ApiToken.Reveal());
            Assert.Equal("new-scopes", organization.Scopes);
            Assert.Equal("https://orgavatar", organization.Avatar);
            Assert.Equal("https://botavatar", organization.BotAvatar);
            Assert.Equal(installEvent.BotAppName, organization.BotAppName);
            Assert.Equal(string.Empty, organization.EnterpriseGridId);

            // Not updated if already set.
            Assert.Equal("old-domain", organization.Domain);
        }

        [Fact]
        public async Task UpdatesExistingEnterpriseOrganizationInfoSuchAsApiToken()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync(
                slug: "slack123",
                platformId: "T001Slack123",
                domain: "old-domain",
                name: "slack",
                apiToken: "old api token",
                scopes: "old scopes");

            organization.BotAppName = "Old Abbot";
            organization.PlatformBotId = null; // This is only updated if it's null.
            await env.Db.SaveChangesAsync();
            var installEvent = new InstallEvent(
                "T001Slack123",
                PlatformType.Slack,
                BotId: "B001",
                BotName: "abbot",
                "Org Name",
                "org-slug",
                ApiToken: new SecretString("new api token", new FakeDataProtectionProvider()),
                EnterpriseId: "E00001234",
                Domain: "new-domain",
                OAuthScopes: "new-scopes",
                BotAppName: "New Abbot",
                BotUserId: "U001",
                BotAvatar: "https://botavatar",
                Avatar: "https://orgavatar"
            );

            var repository = env.Activate<OrganizationRepository>();

            await repository.InstallBotAsync(installEvent);

            await env.ReloadAsync(organization);
            Assert.Equal("B001", organization.PlatformBotId);
            Assert.Equal("U001", organization.PlatformBotUserId);
            Assert.NotNull(organization.ApiToken);
            Assert.Equal("new api token", organization.ApiToken.Reveal());
            Assert.Equal("new-scopes", organization.Scopes);
            Assert.Equal("https://orgavatar", organization.Avatar);
            Assert.Equal("https://botavatar", organization.BotAvatar);
            Assert.Equal(installEvent.BotAppName, organization.BotAppName);
            Assert.Equal(installEvent.EnterpriseId, organization.EnterpriseGridId);

            // Not updated if already set.
            Assert.Equal("old-domain", organization.Domain);
        }
    }

    public class TheGetExpiredTrialsAsyncMethod
    {
        [Fact]
        public async Task ReturnsAnyOrganizationWithAnExpiredTrial()
        {
            var now = new DateTime(2022, 4, 26, 0, 0, 0, DateTimeKind.Utc);

            var env = TestEnvironment.Create();
            var orgWithExpiredTrialInPast = await env.CreateOrganizationAsync();
            orgWithExpiredTrialInPast.Trial = new(PlanType.Beta, now.AddSeconds(-1));
            var orgWithExpiredTrialRightNow = await env.CreateOrganizationAsync();
            orgWithExpiredTrialRightNow.Trial = new(PlanType.Beta, now);
            var orgWithExpiredTrialInFuture = await env.CreateOrganizationAsync();
            orgWithExpiredTrialInFuture.Trial = new(PlanType.Beta, now.AddSeconds(1));
            await env.Db.SaveChangesAsync();

            var repository = env.Activate<OrganizationRepository>();
            var expired = await repository.GetExpiredTrialsAsync(now);

            Assert.Equal(new[] { orgWithExpiredTrialInPast.Id, orgWithExpiredTrialRightNow.Id },
                expired.Select(o => o.Id).ToArray());
        }
    }

    public class TheGetExpiringTrialsAsyncMethod
    {
        [Fact]
        public async Task ReturnsAnyOrganizationThatIsExpiringInTheSetNumberOfDays()
        {
            var now = new DateTime(2022, 4, 26, 0, 0, 0, DateTimeKind.Utc);

            var env = TestEnvironment.Create();
            var orgWithExpiredTrialInPast = await env.CreateOrganizationAsync();
            orgWithExpiredTrialInPast.Trial = new(PlanType.Beta, now.AddSeconds(-1));
            var orgWithExpiredTrialInSevenDays = await env.CreateOrganizationAsync();
            orgWithExpiredTrialInSevenDays.Trial = new(PlanType.Beta, now.AddDays(7).AddHours(3).AddMinutes(5));
            var orgWithExpiredTrialInFuture = await env.CreateOrganizationAsync();
            orgWithExpiredTrialInFuture.Trial = new(PlanType.Beta, now.AddDays(8));
            await env.Db.SaveChangesAsync();

            var repository = env.Activate<OrganizationRepository>();
            var expired = await repository.GetExpiringTrialsAsync(now, 7);

            Assert.Equal(orgWithExpiredTrialInSevenDays.Id, Assert.Single(expired).Id);
        }
    }

    public class TheStartTrialAsyncMethod
    {
        [Theory]
        [InlineData(PlanType.Unlimited)]
        [InlineData(PlanType.Beta)]
        [InlineData(PlanType.Business)]
        [InlineData(PlanType.Team)]
        public async Task ThrowsUnlessOrganizationIsOnTheFreePlan(PlanType planType)
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.PlanType = planType;
            await env.Db.SaveChangesAsync();

            var repo = env.Activate<OrganizationRepository>();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repo.StartTrialAsync(env.TestData.Organization,
                    new(PlanType.Business, DateTime.UtcNow.AddDays(1))));

            Assert.Equal(
                $"Cannot start a trial for the {PlanType.Business} plan because the organization is on the {planType} plan, only organizations on the {PlanType.Free} plan can start trials.",
                ex.Message);
        }

        [Fact]
        public async Task StartsANewTrialForTheOrganization()
        {
            var env = TestEnvironment.Create();
            var org = await env.CreateOrganizationAsync(plan: PlanType.Free);
            var abbot = await env.Organizations.EnsureAbbotMember(org);
            Assert.True(org.TrialEligible);
            await env.Db.SaveChangesAsync();

            var repo = env.Activate<OrganizationRepository>();
            var expiry = DateTime.UtcNow.AddDays(1);
            await repo.StartTrialAsync(org, new(PlanType.Beta, expiry));

            await env.ReloadAsync(env.TestData.Organization);
            Assert.False(env.TestData.Organization.TrialEligible);
            Assert.Equal(PlanType.Beta, org.GetPlan().Type);
            Assert.Equal(PlanType.Free, org.PlanType);
            Assert.Equal(PlanType.Beta, org.Trial?.Plan);
            Assert.Equal(expiry, org.Trial?.Expiry);

            var auditLog = await env.GetAllActivityAsync(org);

            Assert.Collection(auditLog,
                e => {
                    Assert.Equal($"Trial of {PlanType.Beta} plan started.", e.Description);
                    Assert.Equal(abbot.User, e.Actor);
                });

            env.AnalyticsClient.AssertTracked(
                "Trial started",
                AnalyticsFeature.Subscriptions,
                abbot,
                new {
                    trial_plan = PlanType.Beta.ToString(),
                    trial_expiration = expiry,
                }
            );
        }

        [Fact]
        public async Task EndsExistingTrialBeforeStartingANewOne()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.PlanType = PlanType.Free;
            env.TestData.Organization.Trial = new(PlanType.Business, DateTime.UtcNow.AddDays(1));
            await env.Db.SaveChangesAsync();
            Assert.Equal(PlanType.Business, env.TestData.Organization.GetPlan().Type);

            var repo = env.Activate<OrganizationRepository>();
            var expiry = DateTime.UtcNow.AddDays(1);
            await repo.StartTrialAsync(env.TestData.Organization, new(PlanType.Beta, expiry));

            await env.ReloadAsync(env.TestData.Organization);
            Assert.Equal(PlanType.Beta, env.TestData.Organization.GetPlan().Type);
            Assert.Equal(PlanType.Free, env.TestData.Organization.PlanType);
            Assert.Equal(PlanType.Beta, env.TestData.Organization.Trial?.Plan);
            Assert.Equal(expiry, env.TestData.Organization.Trial?.Expiry);

            var auditLog = await env.GetAllActivityAsync();

            Assert.Collection(auditLog,
                e => {
                    Assert.Equal($"Trial of {PlanType.Business} plan ended: New trial started.", e.Description);
                    Assert.Equal(env.TestData.Abbot.User, e.Actor);
                },
                e => {
                    Assert.Equal($"Trial of {PlanType.Beta} plan started.", e.Description);
                    Assert.Equal(env.TestData.Abbot.User, e.Actor);
                });
        }
    }

    public class TheEndTrialAsyncMethod
    {
        [Fact]
        public async Task NoOpsIfOrganizationHasNoTrial()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Trial = null;
            await env.Db.SaveChangesAsync();

            var repo = env.Activate<OrganizationRepository>();
            await repo.EndTrialAsync(env.TestData.Organization, "You can't handle it.", env.TestData.Member);

            await env.ReloadAsync(env.TestData.Organization);
            Assert.Null(env.TestData.Organization.Trial);
            Assert.Empty(await env.AuditLog.GetRecentActivityAsync(env.TestData.Organization));
        }

        [Fact]
        public async Task EndsTheTrialIfOrganizationHasOne()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.PlanType = PlanType.Team;
            env.TestData.Organization.Trial = new(PlanType.Business, DateTime.UtcNow.AddDays(1));
            await env.Db.SaveChangesAsync();
            Assert.Equal(PlanType.Business, env.TestData.Organization.GetPlan().Type);

            var repo = env.Activate<OrganizationRepository>();
            await repo.EndTrialAsync(env.TestData.Organization, "You can't handle it.", env.TestData.Member);

            await env.ReloadAsync(env.TestData.Organization);
            Assert.Equal(PlanType.Team, env.TestData.Organization.GetPlan().Type);
            Assert.Null(env.TestData.Organization.Trial);
            Assert.Collection(await env.AuditLog.GetRecentActivityAsync(env.TestData.Organization),
                e => {
                    Assert.Equal($"Trial of {PlanType.Business} plan ended: You can't handle it.", e.Description);
                    Assert.Equal(env.TestData.User.Id, e.Actor.Id);
                });
        }
    }

    public class TheEnsureAbbotMemberAsyncMethod
    {
        [Fact]
        public async Task ReturnsExistingAbbotMember()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            var abbot = await env.Db.Members.SingleOrDefaultAsync(m =>
                m.User.IsAbbot && m.OrganizationId == organization.Id);

            Assert.NotNull(abbot);
            var repository = env.Activate<OrganizationRepository>();

            var result = await repository.EnsureAbbotMember(organization);

            Assert.NotNull(result);
            Assert.Equal(abbot.Id, result.Id);
        }

        [Fact]
        public async Task CreatesAbbotMember()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync();
            var members = await env.Db.Members.ToListAsync();
            env.Db.Members.RemoveRange(members);
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<OrganizationRepository>();

            var result = await repository.EnsureAbbotMember(organization);

            Assert.NotNull(result);
            Assert.True(result.IsAbbot());
            Assert.Equal(organization.Id, result.OrganizationId);
        }
    }

    public class TheSetAISettingsWithAuditingMethod
    {
        [Theory]
        [InlineData(true, true)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public async Task EnablesAISettingsAndLogsHelpfulMessage(bool enabled, bool ignoreSocial)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            var repository = env.Activate<OrganizationRepository>();

            await repository.SetAISettingsWithAuditing(enabled, ignoreSocial, organization, actor);

            await env.ReloadAsync(organization);
            Assert.NotNull(organization.Settings);
            Assert.Equal(enabled, organization.Settings.AIEnhancementsEnabled);
            Assert.Equal(ignoreSocial, organization.Settings.IgnoreSocialMessages);
            var auditEntry = (await env.AuditLog.GetRecentActivityAsync(organization)).Last();
            Assert.Equal("Changed AI Enhancement Settings.", auditEntry.Description);
            var jsonType = new {
                OldSettings = (OrganizationSettings?)null,
                NewSettings = (OrganizationSettings?)null,
            };

            var auditEvent = Assert.IsType<AuditEvent>(auditEntry);
            Assert.NotNull(auditEvent.SerializedProperties);
            var json = JsonConvert.DeserializeAnonymousType(auditEvent.SerializedProperties, jsonType);
            Assert.Equal(enabled, json.NewSettings?.AIEnhancementsEnabled);
            Assert.Equal(ignoreSocial, json.NewSettings?.IgnoreSocialMessages);
            Assert.Null(json.OldSettings?.AIEnhancementsEnabled);
            Assert.Null(json.OldSettings?.IgnoreSocialMessages);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task DisablesExistingSettings(bool enabled, bool ignoreSocial)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var initialSettings = new OrganizationSettings
            {
                AIEnhancementsEnabled = true,
                IgnoreSocialMessages = true
            };

            organization.Settings = initialSettings;
            var actor = env.TestData.Member;
            var repository = env.Activate<OrganizationRepository>();

            await repository.SetAISettingsWithAuditing(enabled, ignoreSocial, organization, actor);

            await env.ReloadAsync(organization);
            Assert.NotNull(organization.Settings);
            Assert.Equal(enabled, organization.Settings.AIEnhancementsEnabled);
            Assert.Equal(ignoreSocial, organization.Settings.IgnoreSocialMessages);
            var auditEntry = (await env.AuditLog.GetRecentActivityAsync(organization)).Last();
            Assert.Equal("Changed AI Enhancement Settings.", auditEntry.Description);
            var jsonType = new {
                OldSettings = (OrganizationSettings?)null,
                NewSettings = (OrganizationSettings?)null,
            };

            var auditEvent = Assert.IsType<AuditEvent>(auditEntry);
            Assert.NotNull(auditEvent.SerializedProperties);
            var json = JsonConvert.DeserializeAnonymousType(auditEvent.SerializedProperties, jsonType);
            Assert.Equal(enabled, json.NewSettings?.AIEnhancementsEnabled);
            Assert.Equal(ignoreSocial, json.NewSettings?.IgnoreSocialMessages);
            Assert.Equal(initialSettings.AIEnhancementsEnabled, json.OldSettings?.AIEnhancementsEnabled);
            Assert.Equal(initialSettings.IgnoreSocialMessages, json.OldSettings?.IgnoreSocialMessages);
        }
    }

    public class TheSetOverrideRunnerEndpointAsyncMethod
    {
        [Fact]
        public async Task SetsTheOverrideRunnerEndpoint()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<OrganizationRepository>();

            env.TestData.Organization.Settings.SkillEndpoints[CodeLanguage.CSharp] =
                new(new("https://old.example.com"), "5678");

            await repository.SetOverrideRunnerEndpointAsync(env.TestData.Organization,
                CodeLanguage.CSharp,
                new(new("https://example.com"), "1234"),
                env.TestData.Member);

            await env.ReloadAsync(env.TestData.Organization);
            Assert.Equal("https://example.com/", env.TestData.Organization.Settings.SkillEndpoints[CodeLanguage.CSharp].Url.ToString());
            Assert.Equal("1234", env.TestData.Organization.Settings.SkillEndpoints[CodeLanguage.CSharp].ApiToken);

            var auditEntry = (await env.AuditLog.GetRecentActivityAsync(env.TestData.Organization)).Last();
            var auditEvent = Assert.IsType<AuditEvent>(auditEntry);
            Assert.Equal("Changed Custom Endpoint for CSharp Runner", auditEvent.Description);
            Assert.Equal(new(AuditEventType.RunnerEndpointsSubject, AuditOperation.Changed), auditEvent.Type);

            var properties = JsonConvert.DeserializeAnonymousType(auditEvent.SerializedProperties!,
                new {
                    Language = CodeLanguage.CSharp,
                    OldEndpoint = (SkillRunnerEndpoint?)null,
                    NewEndpoint = (SkillRunnerEndpoint?)null,
                });
            Assert.Equal(CodeLanguage.CSharp, properties.Language);
            Assert.Equal("https://old.example.com/", properties.OldEndpoint?.Url.ToString());
            Assert.Equal("5678", properties.OldEndpoint?.ApiToken);
            Assert.Equal("https://example.com/", properties.NewEndpoint?.Url.ToString());
            Assert.Equal("1234", properties.NewEndpoint?.ApiToken);
        }
    }

    public class TheClearOverrideRunnerEndpointAsyncMethod
    {
        [Fact]
        public async Task ClearsTheOverrideRunnerEndpoint()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<OrganizationRepository>();

            env.TestData.Organization.Settings.SkillEndpoints[CodeLanguage.CSharp] =
                new(new("https://old.example.com"), "5678");

            await repository.ClearOverrideRunnerEndpointAsync(env.TestData.Organization, CodeLanguage.CSharp, env.TestData.Member);

            await env.ReloadAsync(env.TestData.Organization);
            Assert.DoesNotContain(
                CodeLanguage.CSharp,
                env.TestData.Organization.Settings.SkillEndpoints.Keys);

            var auditEntry = (await env.AuditLog.GetRecentActivityAsync(env.TestData.Organization)).Last();
            var auditEvent = Assert.IsType<AuditEvent>(auditEntry);
            Assert.Equal("Removed Custom Endpoint for CSharp Runner", auditEvent.Description);
            Assert.Equal(new(AuditEventType.RunnerEndpointsSubject, AuditOperation.Changed), auditEvent.Type);

            var properties = JsonConvert.DeserializeAnonymousType(auditEvent.SerializedProperties!,
                new {
                    Language = CodeLanguage.CSharp,
                    OldEndpoint = (SkillRunnerEndpoint?)null,
                });
            Assert.Equal(CodeLanguage.CSharp, properties.Language);
            Assert.Equal("https://old.example.com/", properties.OldEndpoint?.Url.ToString());
            Assert.Equal("5678", properties.OldEndpoint?.ApiToken);
        }
    }
}
