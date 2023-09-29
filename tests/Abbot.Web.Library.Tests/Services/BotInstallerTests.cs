using System.Security.Claims;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;
using Serious.Cryptography;
using Serious.TestHelpers;

public class BotInstallerTests
{
    public class TheInstallBotAsyncMethod
    {
        public static InstallEvent CreateInstallEvent(string platformId, Member installer) =>
            CreateInstallEvent(platformId, new FakeClaimsPrincipal(installer));

        public static InstallEvent CreateInstallEvent(string platformId, ClaimsPrincipal? installer = null)
        {
            return new InstallEvent(
                platformId,
                PlatformType.Slack,
                "bot-id",
                "bot-name",
                "orgname",
                "orgslug",
                new SecretString("apitoken", new FakeDataProtectionProvider()),
                EnterpriseId: null,
                "domain",
                "user:read,user:write",
                "bot-app-name",
                "bot-avatar",
                "org-avatar",
                "bot-user-id",
                "app-id",
                installer);
        }

        [Fact]
        public async Task CreatesNewOrgWithBusinessPlanTrialWhenNoExistingOrg()
        {
            var now = new DateTime(2022, 4, 27, 0, 0, 0, DateTimeKind.Utc);
            var env = TestEnvironment.CreateWithoutData();
            env.Clock.TravelTo(now);

            var installerPrincipal = new FakeClaimsPrincipal("TNEWORG");
            var installEvent = CreateInstallEvent("TNEWORG", installerPrincipal);
            var installer = env.Activate<BotInstaller>();
            await installer.InstallBotAsync(installEvent);

            var createdOrg =
                await env.Db.Organizations
                    .Include(o => o.Members)
                    .ThenInclude(m => m.User)
                    .SingleAsync(o => o.PlatformId == installEvent.PlatformId);

            Assert.Equal(installEvent.PlatformId, createdOrg.PlatformId);
            Assert.Equal(installEvent.PlatformType, createdOrg.PlatformType);
            Assert.Equal(installEvent.BotId, createdOrg.PlatformBotId);
            Assert.Equal(installEvent.BotName, createdOrg.BotName);
            Assert.Equal(installEvent.Name, createdOrg.Name);
            Assert.Equal(installEvent.Slug, createdOrg.Slug);
            Assert.Equal(installEvent.ApiToken?.Reveal(), createdOrg.ApiToken?.Reveal());
            Assert.Equal(installEvent.Domain, createdOrg.Domain);
            Assert.Equal(installEvent.OAuthScopes, createdOrg.Scopes);
            Assert.Equal(installEvent.BotAppName, createdOrg.BotAppName);
            Assert.Equal(installEvent.BotUserId, createdOrg.PlatformBotUserId);
            Assert.Equal(installEvent.BotAvatar, createdOrg.BotAvatar);
            Assert.Equal(installEvent.Avatar, createdOrg.Avatar);
            Assert.Equal(installEvent.AppId, createdOrg.BotAppId);
            Assert.Equal(PlanType.Free, createdOrg.PlanType);
            Assert.Equal(PlanType.Business, createdOrg.Trial?.Plan);
            Assert.Equal(now + TrialPlan.TrialLength, createdOrg.Trial?.Expiry);

            Assert.Collection(createdOrg.Members,
                m => Assert.True(m.User.IsAbbot),
                m => Assert.Equal(installerPrincipal.GetPlatformUserId(), m.User.PlatformUserId));

            var abbotMember = createdOrg.Members[0];
            var installerMember = createdOrg.Members[1];

            var auditEvents = await env.AuditLog.GetRecentActivityAsync(
                createdOrg,
                activityTypeFilter: ActivityTypeFilter.All);
            Assert.Collection(auditEvents,
                e => {
                    Assert.Equal(abbotMember.User, e.Actor);
                    Assert.Equal("Trial of Business plan started.", e.Description);
                },
                e => {
                    Assert.IsType<InstallationEvent>(e);
                    Assert.Equal(installerMember.User, e.Actor);
                    Assert.Equal($"Installed bot-app-name to the {createdOrg.Name} {createdOrg.PlatformType}.", e.Description);
                });

            var slackApp = await env.Integrations.GetIntegrationAsync(createdOrg, IntegrationType.SlackApp);
            Assert.Null(slackApp);

            env.AnalyticsClient.AssertTracked("Slack App Installed", AnalyticsFeature.Activations, installerMember,
                new {
                    app_id = installEvent.AppId,
                });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)] // Even if not marked trial eligible, foreign orgs get a trial.
        public async Task ConvertsForeignOrgToHomeOrgAndActivatesBusinessPlanTrial(bool trialEligible)
        {
            var now = new DateTime(2022, 4, 27, 0, 0, 0, DateTimeKind.Utc);
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(now);

            var installerMember = env.TestData.ForeignMember;
            env.TestData.ForeignOrganization.TrialEligible = trialEligible;
            Assert.Equal(PlanType.None, env.TestData.ForeignOrganization.PlanType);
            await env.Db.SaveChangesAsync();

            var installEvent = CreateInstallEvent(env.TestData.ForeignOrganization.PlatformId, installerMember);
            var installer = env.Activate<BotInstaller>();
            await installer.InstallBotAsync(installEvent);

            var updatedOrg = env.TestData.ForeignOrganization;
            await env.ReloadAsync(updatedOrg);

            Assert.Equal(installEvent.PlatformId, updatedOrg.PlatformId);
            Assert.Equal(installEvent.PlatformType, updatedOrg.PlatformType);
            Assert.Equal(installEvent.BotName, updatedOrg.BotName);
            Assert.Equal(installEvent.Name, updatedOrg.Name);
            Assert.Equal(installEvent.Slug, updatedOrg.Slug);
            Assert.Equal(installEvent.ApiToken?.Reveal(), updatedOrg.ApiToken?.Reveal());
            Assert.Equal(installEvent.OAuthScopes, updatedOrg.Scopes);
            Assert.Equal(installEvent.BotUserId, updatedOrg.PlatformBotUserId);
            Assert.Equal(installEvent.AppId, updatedOrg.BotAppId);
            Assert.Equal(installEvent.BotAppName, updatedOrg.BotAppName);
            Assert.Equal(installEvent.BotId, updatedOrg.PlatformBotId);

            // Not updated if already set.
            Assert.NotEqual(installEvent.Domain, updatedOrg.Domain);

            Assert.Equal(PlanType.Free, updatedOrg.PlanType);
            Assert.Equal(PlanType.Business, updatedOrg.Trial?.Plan);
            Assert.Equal(now + TrialPlan.TrialLength, updatedOrg.Trial?.Expiry);

            var auditEvents = await env.GetAllActivityAsync(updatedOrg);
            Assert.Collection(auditEvents,
                e => {
                    Assert.IsType<InstallationEvent>(e);
                    Assert.Equal(installerMember.User, e.Actor);
                    Assert.Equal($"Installed bot-app-name to the {updatedOrg.Name} {updatedOrg.PlatformType}.", e.Description);
                },
                e => {
                    Assert.Equal(env.TestData.ForeignAbbot.User, e.Actor);
                    Assert.Equal("Trial of Business plan started.", e.Description);
                });

            var slackApp = await env.Integrations.GetIntegrationAsync(updatedOrg, IntegrationType.SlackApp);
            Assert.Null(slackApp);

            env.AnalyticsClient.AssertTracked("Slack App Installed", AnalyticsFeature.Activations, installerMember,
                new {
                    app_id = installEvent.AppId,
                });
        }

        [Theory]
        [InlineData(true, PlanType.Unlimited)]
        [InlineData(true, PlanType.Beta)]
        [InlineData(true, PlanType.Business)]
        [InlineData(true, PlanType.Team)]
        [InlineData(false, PlanType.Unlimited)]
        [InlineData(false, PlanType.Beta)]
        [InlineData(false, PlanType.Business)]
        [InlineData(false, PlanType.Team)]
        [InlineData(false, PlanType.Free)]
        public async Task DoesNotStartTrialUnlessOrgIsTrialEligibleAndOnFreePlan(bool trialEligible, PlanType planType)
        {
            var now = new DateTime(2022, 4, 27, 0, 0, 0, DateTimeKind.Utc);
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(now);

            var org = env.TestData.Organization;
            org.TrialEligible = trialEligible;
            org.PlanType = planType;
            await env.Db.SaveChangesAsync();

            var installEvent = CreateInstallEvent(org.PlatformId);
            var installer = env.Activate<BotInstaller>();
            await installer.InstallBotAsync(installEvent);

            await env.ReloadAsync(org);

            Assert.Equal(planType, org.PlanType);
            Assert.Null(org.Trial);
        }

        [Fact]
        public async Task StartsTrialForTrialEligibleOrgOnFreePlan()
        {
            var now = new DateTime(2022, 4, 27, 0, 0, 0, DateTimeKind.Utc);
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(now);

            var org = env.TestData.Organization;
            Assert.False(org.Settings?.AIEnhancementsEnabled is true);
            org.TrialEligible = true;
            org.PlanType = PlanType.Free;
            await env.Db.SaveChangesAsync();

            var installEvent = CreateInstallEvent(org.PlatformId);
            var installer = env.Activate<BotInstaller>();
            await installer.InstallBotAsync(installEvent);

            await env.ReloadAsync(org);

            Assert.Equal(PlanType.Free, org.PlanType);
            Assert.Equal(PlanType.Business, org.Trial?.Plan);
            Assert.Equal(now + TrialPlan.TrialLength, org.Trial?.Expiry);
            Assert.True(org.Settings?.AIEnhancementsEnabled is true);
        }

        [Fact]
        public async Task IgnoresIntegrationIfInstallEventDoesNotMatchCustomSlackAppId()
        {
            var env = TestEnvironment.Create();
            var org = env.TestData.Organization;

            var slackApp = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(slackApp.Enabled);

            var installEvent = CreateInstallEvent(org.PlatformId);
            slackApp.ExternalId = "A98765";
            await env.Integrations.SaveSettingsAsync(slackApp, new SlackAppSettings());

            var installer = env.Activate<BotInstaller>();
            await installer.InstallBotAsync(installEvent);

            await env.ReloadAsync(slackApp);
            Assert.False(slackApp.Enabled);
        }

        [Fact]
        public async Task EnablesIntegrationIfInstallEventMatchesCustomSlackAppId()
        {
            var env = TestEnvironment.Create();
            var org = env.TestData.Organization;

            var slackApp = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(slackApp.Enabled);

            var installEvent = CreateInstallEvent(org.PlatformId);
            slackApp.ExternalId = installEvent.AppId;
            await env.Integrations.SaveSettingsAsync(slackApp, new SlackAppSettings());

            var installer = env.Activate<BotInstaller>();
            await installer.InstallBotAsync(installEvent);

            await env.ReloadAsync(org, slackApp);
            Assert.True(slackApp.Enabled);

            var auditEvents = await env.GetAllActivityAsync(org);
            Assert.Collection(auditEvents,
                e => {
                    Assert.IsType<InstallationEvent>(e);
                    Assert.Equal(env.TestData.SystemAbbot.User, e.Actor);
                    Assert.Equal($"Installed bot-app-name to the {org.Name} {org.PlatformType}.", e.Description);
                },
                e => {
                    Assert.Equal(env.TestData.SystemAbbot.User, e.Actor);
                    Assert.Equal("Enabled Custom Slack App integration.", e.Description);
                });
        }
    }

    public class TheUninstallBotAsyncMethod
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("A01234", null)]
        [InlineData("A56789", null)]
        [InlineData("A56789", "U001")]
        public async Task DoesNothingForNotInstalledBotWithoutIntegration(string? botAppId, string? botUserId)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            Assert.Null(organization.LastPlatformUpdate);

            var oldBot = BotChannelUser.GetBotUser(organization);

            // Different BotAppId, or matching BotAppId (for reinstall) but missing BotUserId
            var initialOrgAuth = new SlackAuthorization(botAppId, BotUserId: botUserId);
            initialOrgAuth.Apply(organization);
            Assert.Equal(botAppId, organization.BotAppId);
            Assert.Equal(botUserId, organization.PlatformBotUserId);
            Assert.Null(organization.PlatformBotId);
            await env.Organizations.SaveChangesAsync();

            var installer = env.Activate<BotInstaller>();

            var evt = new PlatformEvent<UninstallPayload>(
                new UninstallPayload(organization.PlatformId, "A01234"),
                null,
                oldBot,
                env.Clock.UtcNow.AddSeconds(-1),
                new FakeResponder(),
                env.TestData.Member,
                null,
                organization);

            await installer.UninstallBotAsync(evt);

            await env.ReloadAsync(organization);
            Assert.Equal(initialOrgAuth, new SlackAuthorization(organization));
            Assert.Null(organization.LastPlatformUpdate);

            await env.AuditLog.AssertNoRecent<InstallationEvent>();
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(false, null)]
        [InlineData(false, "Aabbot")]
        [InlineData(true, null)]
        [InlineData(true, "Aabbot")]
        public async Task UninstallsInstalledBotWithoutAuthorizedIntegration(bool? integrationEnabled, string? integrationAppId)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            Assert.NotNull(organization.PlatformBotId);
            Assert.NotNull(organization.PlatformBotUserId);
            Assert.NotNull(organization.ApiToken);

            env.Clock.Freeze();

            var slackApp = integrationEnabled is null
                ? null
                : await env.Integrations.EnsureIntegrationAsync(organization, IntegrationType.SlackApp);

            if (slackApp is not null)
            {
                slackApp.Enabled = integrationEnabled.GetValueOrDefault();
                slackApp.ExternalId = integrationAppId;
                await env.Integrations.SaveSettingsAsync(slackApp,
                    new SlackAppSettings
                    {
                        Authorization = null,
                        DefaultAuthorization = null,
                    });
            }

            var installer = env.Activate<BotInstaller>();

            var evt = new PlatformEvent<UninstallPayload>(
                new UninstallPayload(organization.PlatformId, organization.BotAppId.Require()),
                null,
                BotChannelUser.GetBotUser(organization),
                env.Clock.UtcNow.AddSeconds(-1),
                new FakeResponder(),
                env.TestData.Member,
                null,
                organization);

            await installer.UninstallBotAsync(evt);

            await env.ReloadAsync(organization, slackApp);
            // Leave old AppId/AppName intact so we know which app to Reinstall
            Assert.Equal(new SlackAuthorization("Aabbot", "Abbot App"), new(organization));
            Assert.Equal(env.Clock.UtcNow, organization.LastPlatformUpdate);

            // Leave enabled, if appropriate, so Reinstall will Just Work
            Assert.Equal(integrationEnabled, slackApp?.Enabled);

            var auditEvents = await env.AuditLog.GetRecentActivityAsync(organization);
            Assert.Collection(auditEvents,
                e => {
                    Assert.IsType<InstallationEvent>(e);
                    Assert.Equal(env.TestData.Member.User.Id, e.ActorId);
                    Assert.Equal($"Uninstalled Abbot App from the {organization.Name} {organization.PlatformType}.", e.Description);
                });
        }

        [Theory]
        [InlineData(false, "Aabbot", "Abbot App")]
        [InlineData(false, "Acustom_abbot", "Custom Abbot")]
        [InlineData(true, "Aabbot", "Abbot App")]
        [InlineData(true, "Acustom_abbot", "Custom Abbot")]
        public async Task UninstallsInstalledBotWithAuthorizedIntegration(bool integrationEnabled, string uninstallAppId, string expectedUninstalledAppName)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            Assert.NotNull(organization.PlatformBotId);
            Assert.NotNull(organization.PlatformBotUserId);
            Assert.NotNull(organization.ApiToken);

            env.Clock.Freeze();

            var slackApp = await env.Integrations.EnsureIntegrationAsync(organization, IntegrationType.SlackApp);
            slackApp.Enabled = integrationEnabled;
            var defaultAuth = new SlackAuthorization(organization);
            var customAuth = env.CreateSlackAuthorization("Acustom_abbot", "Custom Abbot");
            slackApp.ExternalId = customAuth.AppId;
            await env.Integrations.SaveSettingsAsync(slackApp,
                new SlackAppSettings
                {
                    Authorization = customAuth,
                    DefaultAuthorization = defaultAuth,
                });

            if (integrationEnabled)
            {
                customAuth.Apply(organization);
                await env.Organizations.SaveChangesAsync();
            }
            var initialOrgAuth = new SlackAuthorization(organization);

            var installer = env.Activate<BotInstaller>();

            var evt = new PlatformEvent<UninstallPayload>(
                new UninstallPayload(organization.PlatformId, uninstallAppId),
                null,
                BotChannelUser.GetBotUser(organization),
                env.Clock.UtcNow.AddSeconds(-1),
                new FakeResponder(),
                env.TestData.Member,
                null,
                organization);

            await installer.UninstallBotAsync(evt);

            await env.ReloadAsync(organization, slackApp);

            // Leave old AppId/AppName intact so we know which app to Reinstall
            var expectedOrgAuth = initialOrgAuth.AppId == uninstallAppId
                ? new(initialOrgAuth.AppId, initialOrgAuth.AppName)
                : initialOrgAuth;
            Assert.Equal(expectedOrgAuth, new(organization));
            Assert.Equal(env.Clock.UtcNow, organization.LastPlatformUpdate);

            // Leave enabled, if appropriate, so Reinstall will Just Work
            Assert.Equal(integrationEnabled, slackApp.Enabled);

            var updatedSettings = env.Integrations.ReadSettings<SlackAppSettings>(slackApp);
            Assert.Equal(customAuth.AppId, slackApp.ExternalId);
            Assert.Equal(uninstallAppId == customAuth.AppId ? null : customAuth, updatedSettings.Authorization);
            Assert.Equal(uninstallAppId == defaultAuth.AppId ? null : defaultAuth, updatedSettings.DefaultAuthorization);

            var auditEvents = await env.AuditLog.GetRecentActivityAsync(organization);
            Assert.Collection(auditEvents,
                e => {
                    Assert.IsType<InstallationEvent>(e);
                    Assert.Equal(env.TestData.Member.User.Id, e.ActorId);
                    Assert.Equal($"Uninstalled {expectedUninstalledAppName} from the {organization.Name} {organization.PlatformType}.", e.Description);
                });
        }
    }
}
