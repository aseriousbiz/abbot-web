using Abbot.Common.TestHelpers;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack.Manifests;

namespace Abbot.Web.Library.Tests.Integrations.SlackApp;

public class SlackIntegrationTests
{
    public class TheGetDefaultManifestAsyncMethod
    {
        [Fact]
        public async Task ReturnsExpectedManifest()
        {
            var env = TestEnvironment.CreateWithoutData();

            var slack = env.Get<ISlackIntegration>();

            var manifest = await slack.GetDefaultManifestAsync();

            var d = manifest.DisplayInformation;
            Assert.NotNull(d);

            Assert.Equal("Abbot", d.Name);
            Assert.NotNull(d.Description);
            Assert.NotNull(d.LongDescription);
            Assert.NotNull(d.BackgroundColor);

            var f = manifest.Features;
            Assert.NotNull(f);

            Assert.NotNull(f.AppHome);
            Assert.True(f.AppHome.HomeTabEnabled);
            Assert.True(f.AppHome.MessagesTabEnabled);
            Assert.False(f.AppHome.MessagesTabReadOnlyEnabled);

            Assert.NotNull(f.BotUser);
            Assert.Equal("abbot", f.BotUser.DisplayName);
            Assert.True(f.BotUser.AlwaysOnline);

            Assert.NotNull(f.Shortcuts);
            Assert.Collection(f.Shortcuts,
                s => {
                    Assert.Equal("Manage Conversation", s.Name);
                    Assert.Equal(ManifestShortcutType.Message, s.Type);
                    Assert.NotNull(s.Description);

                    var handler = new InteractionCallbackInfo(nameof(ManageConversationHandler));
                    Assert.Equal(handler.ToString(), s.CallbackId);
                });

            var o = manifest.OAuthConfig;
            Assert.NotNull(o);

            Assert.Equal(
                new()
                {
                    // Used to authenticate existing Auth0 users
                    "https://app.ab.bot/slack/install/complete",
                    // Used for initial Auth0 consent
                    "https://aseriousbiz.us.auth0.com/login/callback",
                },
                o.RedirectUrls);

            Assert.NotNull(o.Scopes);
            Assert.NotNull(o.Scopes.User);
            Assert.Equal(new[] { "email", "openid", "profile" }, o.Scopes.User);

            // Slack always sorts (not quite alphabetical!) scope lists; might as well match
            var expectedScopes = CommonTestData.DefaultOrganizationScopes.Split(',');
            Assert.Equal(expectedScopes, o.Scopes.Bot?.ToArray());

            var s = manifest.Settings;
            Assert.NotNull(s);

            Assert.NotNull(s.EventSubscriptions);
            Assert.Equal("https://in.ab.bot/api/slack", s.EventSubscriptions.RequestUrl);

            Assert.NotNull(s.EventSubscriptions.BotEvents);
            Assert.Contains("app_home_opened", s.EventSubscriptions.BotEvents);
            Assert.Contains("app_mention", s.EventSubscriptions.BotEvents);
            Assert.Equal(
                new[]
                {
                    "app_home_opened",
                    "app_mention",
                    "app_uninstalled",
                    "channel_archive",
                    "channel_deleted",
                    "channel_left",
                    "channel_rename",
                    "channel_unarchive",
                    "email_domain_changed",
                    "group_archive",
                    "group_deleted",
                    "group_left",
                    "group_rename",
                    "group_unarchive",
                    "member_joined_channel",
                    "member_left_channel",
                    "message.channels",
                    "message.groups",
                    "message.im",
                    "message.mpim",
                    "reaction_added",
                    "reaction_removed",
                    "shared_channel_invite_accepted",
                    "shared_channel_invite_approved",
                    "shared_channel_invite_declined",
                    "team_domain_change",
                    "team_join",
                    "team_rename",
                    "tokens_revoked",
                    "user_change",
                },
                s.EventSubscriptions.BotEvents);
            Assert.Contains("reaction_added", s.EventSubscriptions.BotEvents);
            Assert.Null(s.EventSubscriptions.UserEvents);

            Assert.NotNull(s.Interactivity);
            Assert.True(s.Interactivity.IsEnabled);
            Assert.Equal("https://in.ab.bot/api/slack", s.Interactivity.RequestUrl);
            Assert.Equal("https://in.ab.bot/api/slack", s.Interactivity.MessageMenuOptionsUrl);

            Assert.Null(s.AllowedIpAddressRanges);
            Assert.False(s.OrgDeployEnabled);
            Assert.False(s.SocketModeEnabled);
            Assert.False(s.TokenRotationEnabled);
        }
    }

    public class TheGenerateManifestMethod
    {
        public static TheoryData<SlackManifestSettings?> InvalidSlackManifestSettings => new()
        {
            null,
            new(),
            new() { AppName = null, BotUserDisplayName = "Bot!" },
            new() { AppName = "App!", BotUserDisplayName = null },
        };

        [Theory]
        [MemberData(nameof(InvalidSlackManifestSettings))]
        public void ReturnsNullForInvalidSettings(SlackManifestSettings? settings)
        {
            var env = TestEnvironment.CreateWithoutData();

            var slack = env.Get<ISlackIntegration>();

            var result = slack.GenerateManifest(
                new() { DisplayInformation = new("Name!") },
                new() { Id = 42 },
                new() { Manifest = settings });

            Assert.Null(result);
        }

        [Fact]
        public void ReturnsManifestWithSpecificSettingsReplaced()
        {
            var env = TestEnvironment.CreateWithoutData();

            var slack = env.Get<ISlackIntegration>();

            var manifest = new Manifest
            {
                DisplayInformation = new(
                    Name: "Replace me!",
                    Description: "Keep me!",
                    LongDescription: "And keep me!",
                    BackgroundColor: "#000"),
                Features = new()
                {
                    AppHome = new(true, true, false),
                    BotUser = new("Replace me, too!", AlwaysOnline: false),
                    Shortcuts = new()
                    {
                        new("Keep", ManifestShortcutType.Message, "cb", "d"),
                    },
                },
                OAuthConfig = new()
                {
                    RedirectUrls = new() { "https://example.com/callback" },
                    Scopes = new()
                    {
                        User = new() { "email " },
                        Bot = new() { "chat" },
                    },
                },
                Settings = new()
                {
                    AllowedIpAddressRanges = new() { "keep" },
                    EventSubscriptions = new()
                    {
                        RequestUrl = "https://example.com/callme",
                        BotEvents = new() { "be" },
                        UserEvents = new() { "ue" },
                    },
                    Interactivity = new()
                    {
                        IsEnabled = true,
                        RequestUrl = "https://example.com/callme",
                        MessageMenuOptionsUrl = "https://example.com/callme",
                    },
                },
            };

            var integration = new Integration { Id = 42 };
            var settings = new SlackAppSettings
            {
                Manifest = new SlackManifestSettings
                {
                    AppName = "App!",
                    BotUserDisplayName = "Bot!",
                },
            };

            var result = slack.GenerateManifest(manifest, integration, settings);

            Assert.NotNull(result);
            Assert.Equal(
                 new("App!",
                    Description: "Keep me!",
                    LongDescription: "And keep me!",
                    BackgroundColor: "#000"),
                 result.DisplayInformation);

            Assert.NotNull(result.Features);
            Assert.Equal(manifest.Features.AppHome, result.Features.AppHome);
            Assert.Equal(new("Bot!", AlwaysOnline: false), result.Features.BotUser);
            Assert.Equal(manifest.Features.Shortcuts, result.Features.Shortcuts);

            Assert.NotNull(result.OAuthConfig);
            Assert.Equal(
                new()
                {
                    "https://app.ab.bot/slack/installed",
                    "https://aserioustest.us.auth0.com/login/callback",
                },
                result.OAuthConfig.RedirectUrls);
            Assert.Equal(manifest.OAuthConfig.Scopes, result.OAuthConfig.Scopes);

            Assert.NotNull(result.Settings);
            // We don't care about this at all, but keep if present
            Assert.Equal(manifest.Settings.AllowedIpAddressRanges, result.Settings.AllowedIpAddressRanges);

            Assert.NotNull(result.Settings.EventSubscriptions);
            Assert.Equal("https://app.ab.bot/api/slack?integrationId=42", result.Settings.EventSubscriptions.RequestUrl);
            Assert.Equal(manifest.Settings.EventSubscriptions.BotEvents, result.Settings.EventSubscriptions.BotEvents);
            Assert.Equal(manifest.Settings.EventSubscriptions.UserEvents, result.Settings.EventSubscriptions.UserEvents);

            Assert.NotNull(result.Settings.Interactivity);
            Assert.True(result.Settings.Interactivity.IsEnabled);
            Assert.Equal("https://app.ab.bot/api/slack?integrationId=42", result.Settings.Interactivity.RequestUrl);
            Assert.Equal("https://app.ab.bot/api/slack?integrationId=42", result.Settings.Interactivity.MessageMenuOptionsUrl);
        }
    }

    public class TheGetAuthorizationAsyncMethod
    {
        public class WithoutIntegrationId
        {
            [Fact]
            public async Task ReturnsAuthFromOrgWithoutIntegration()
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                Assert.Null(await env.Integrations.GetIntegrationAsync(org, IntegrationType.SlackApp));

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, null);

                AssertOrgAuthorization(org, auth);
            }

            [Theory]
            [InlineData(null, false)]
            [InlineData(null, true)] // Should be false without SlackAppId, but just in case
            [InlineData("A1", false)]
            [InlineData("A1", true)] // Should be false without SlackAppId, but just in case
            public async Task ReturnsAuthFromOrgWithIntegrationWithoutAppId(string? orgAppId, bool enabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                // Test data includes null (not installed) and a real AppId.
                // If the Integration SlackAppId is null, we need to make sure matching a null org.BotAppId
                // isn't interpreted as a custom app being enabled.
                org.BotAppId = orgAppId;

                var integration = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp, enabled);
                integration.ExternalId = null;
                await env.Integrations.SaveSettingsAsync(integration, new SlackAppSettings());

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, null);

                AssertOrgAuthorization(org, auth);
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)] // Should be false if Org AppId doesn't match, but just in case
            public async Task ReturnsAuthFromOrgWithIntegrationWithoutMatchingAppId(bool enabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                var integration = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp, enabled);
                integration.ExternalId = "Acustom";
                await env.Integrations.SaveSettingsAsync(integration, new SlackAppSettings());

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, null);

                AssertOrgAuthorization(org, auth);
            }

            [Theory]
            [InlineData(false)] // Should be true if Org AppId matches, but just in case
            [InlineData(true)]
            public async Task ReturnsDefaultAuthFromIntegrationWithMatchingAppIdAndDefaultAuth(bool enabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                var integration = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp, enabled);
                integration.ExternalId = org.BotAppId;
                var settings = await env.Integrations.SaveSettingsAsync(integration,
                    new SlackAppSettings
                    {
                        DefaultAuthorization = new(
                            AppId: org.BotAppId,
                            BotId: "B42"),
                    });

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, null);

                Assert.Equal(settings.DefaultAuthorization, auth);
            }

            [Theory]
            [InlineData(false)] // Should be true if Org AppId matches, but just in case
            [InlineData(true)]
            public async Task ReturnsEmptyAuthForIntegrationWithMatchingAppIdButNoDefaultAuth(bool enabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                var integration = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp, enabled);
                integration.ExternalId = org.BotAppId;
                await env.Integrations.SaveSettingsAsync(integration,
                    new SlackAppSettings
                    {
                        DefaultAuthorization = null,
                    });

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, null);

                AssertEmptyAuthorization(auth);

                var message = Assert.Single(env.GetAllLogs<SlackIntegration>());
                Assert.Equal(message.LogLevel, LogLevel.Warning);
                Assert.Equal(message.Message, $"Default Slack Authorization missing for {org.PlatformId} (Integration {integration.Id})");
            }
        }

        public class WithIntegrationId
        {
            [Theory]
            [InlineData(null)]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsEmptyAuthForOrgWithoutIntegration(bool? foreignOrgEnabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                var foreignIntegration = foreignOrgEnabled is null
                    ? null
                    : await env.Integrations.EnsureIntegrationAsync(
                        env.TestData.ForeignOrganization,
                        IntegrationType.SlackApp,
                        foreignOrgEnabled.Value);

                Assert.Null(await env.Integrations.GetIntegrationAsync(org, IntegrationType.SlackApp));

                // Should ignore matching foreign Integration, Enabled or not
                var integrationId = foreignIntegration?.Id ?? 9999;

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, integrationId);

                AssertEmptyAuthorization(auth);

                var message = Assert.Single(env.GetAllLogs<SlackIntegration>());
                Assert.Equal(message.LogLevel, LogLevel.Warning);
                Assert.Equal(message.Message, $"Callback integrationId ({integrationId}) not found for {org.PlatformId}");
            }

            [Theory]
            [InlineData(false, null)]
            [InlineData(false, false)]
            [InlineData(false, true)]
            [InlineData(true, null)]
            [InlineData(true, false)]
            [InlineData(true, true)]
            public async Task ReturnsEmptyAuthForOrgWithIntegrationButWrongIntegrationId(bool enabled, bool? foreignOrgEnabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                var integration = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp, enabled);

                // Should ignore matching foreign Integration, Enabled or not
                var foreignIntegration = foreignOrgEnabled is null
                    ? null
                    : await env.Integrations.EnsureIntegrationAsync(
                        env.TestData.ForeignOrganization,
                        IntegrationType.SlackApp,
                        foreignOrgEnabled.Value);

                // Should ignore matching foreign Integration, Enabled or not
                var integrationId = foreignIntegration?.Id ?? 9999;

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, integrationId);

                AssertEmptyAuthorization(auth);

                var message = Assert.Single(env.GetAllLogs<SlackIntegration>());
                Assert.Equal(message.LogLevel, LogLevel.Warning);
                Assert.Equal(message.Message, $"Callback integrationId ({integrationId}) does not match {org.PlatformId} (Integration {integration.Id})");
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsEmptyAuthForMatchingIntegrationIdButMissingAuthorization(bool enabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                var integration = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp, enabled);
                await env.Integrations.SaveSettingsAsync(integration,
                    new SlackAppSettings()
                    {
                        Authorization = null,
                    });

                var integrationId = integration.Id;

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, integrationId);

                AssertEmptyAuthorization(auth);

                var message = Assert.Single(env.GetAllLogs<SlackIntegration>());
                Assert.Equal(message.LogLevel, LogLevel.Warning);
                Assert.Equal(message.Message, $"Custom Slack Authorization missing for {org.PlatformId} (Integration {integration.Id})");
            }

            [Theory]
            [InlineData(false)]
            [InlineData(true)]
            public async Task ReturnsIntegrationAuthForMatchingIntegrationId(bool enabled)
            {
                var env = TestEnvironment.Create();
                var org = env.TestData.Organization;

                var integration = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp, enabled);
                var settings = await env.Integrations.SaveSettingsAsync(integration,
                    new SlackAppSettings()
                    {
                        Authorization = new(
                            AppId: "Acustom",
                            BotId: "Bcustom",
                            BotUserId: "Ucustom"),
                    });

                var integrationId = integration.Id;

                var slack = env.Get<ISlackIntegration>();

                var auth = await slack.GetAuthorizationAsync(org, integrationId);

                Assert.Equal(settings.Authorization, auth);
            }
        }

        static void AssertEmptyAuthorization(SlackAuthorization auth)
        {
            Assert.Equal(new(), auth);
        }

        static void AssertOrgAuthorization(Organization org, SlackAuthorization auth)
        {
            Assert.Equal(org.BotAppId, auth.AppId);
            Assert.Equal(org.BotAppName, auth.AppName);
            Assert.Equal(org.PlatformBotId, auth.BotId);
            Assert.Equal(org.PlatformBotUserId, auth.BotUserId);
            Assert.Equal(org.BotName, auth.BotName);
            Assert.Equal(org.ApiToken, auth.ApiToken);
        }
    }
}
