using System.Diagnostics;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Abbot.Security;
using Serious.Slack.AspNetCore;
using Xunit;

namespace Abbot.Web.Library.Tests.Messaging;

public class SlackAuthenticatorTests
{
    public class TheGetCredentialsAsyncMethod
    {
        [Theory]
        [InlineData(null, null, "cs-config", "sd", "sc")]
        [InlineData(null, "ci-config", null, "sd", "sc")]
        [InlineData(null, "ci-config", "cs-config", null, "sc")] // Custom scopes ignored
        [InlineData("A12", null, "cs-config", "sd", "sc")]
        [InlineData("A12", "ci-config", null, "sd", "sc")]
        [InlineData("A12", "ci-config", "cs-config", null, "sc")] // Custom scopes ignored
        [InlineData("A34", null, "cs-config", "sd", null)]
        [InlineData("A34", "ci-config", null, "sd", null)]
        [InlineData("A34", "ci-config", "cs-config", null, null)]
        //         ("A34", "ci-config", "cs-config", "sd", null)  // Default scopes are fallback
        //         ("A34", "ci-config", "cs-config", null, "sc")  // Custom scopes _not_ ignored
        public async Task ThrowsForMissingConfig(
            string? installedAppId,
            string? clientId,
            string? clientSecret,
            string? scopesDefault,
            string? scopesCustom)
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService(Options.Create(new SlackOptions()
                {
                    AppId = "A12",
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    RequiredScopes = scopesDefault,
                    CustomAppScopes = scopesCustom,
                }))
                .Build();
            var org = env.TestData.Organization;
            org.BotAppId = installedAppId;
            await env.Db.SaveChangesAsync();

            var authenticator = env.Activate<SlackAuthenticator>();

            await Assert.ThrowsAsync<UnreachableException>(() =>
                authenticator.GetCredentialsAsync(org, OAuthAction.Install)
            );
        }

        [Theory]
        [InlineData(null, "cs", "sd", "sc")]
        [InlineData("ci", null, "sd", "sc")]
        [InlineData("ci", "cs", null, null)]
        //         ("ci", "cs", null, "sc") // Custom scopes have precedence
        //         ("ci", "cs", "sd", null) // Default scopes are fallback
        //         ("ci", "cs", "sd", "sc") // Custom scopes have precedence
        public async Task ThrowsForInstalledIntegrationButMissingCustomConfig(
            string? clientId,
            string? clientSecret,
            string? scopesDefault,
            string? scopesCustom)
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService(Options.Create(new SlackOptions()
                {
                    AppId = "A012345",
                    ClientId = "ci-config",
                    ClientSecret = "cs-config",
                    RequiredScopes = scopesDefault,
                    CustomAppScopes = scopesCustom,
                }))
                .Build();
            var org = env.TestData.Organization;
            org.BotAppId = "A98765";
            var app = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(app.Enabled); // Ignored
            app.ExternalId = org.BotAppId; // Installed
            await env.Integrations.SaveSettingsAsync(app,
                new SlackAppSettings
                {
                    Credentials = new SlackCredentials
                    {
                        ClientId = clientId,
                        ClientSecret = env.Secret(clientSecret),
                        SigningSecret = env.Secret("ignored"),
                    },
                });

            var authenticator = env.Activate<SlackAuthenticator>();

            await Assert.ThrowsAsync<UnreachableException>(() =>
                authenticator.GetCredentialsAsync(org, OAuthAction.Install)
            );
        }

        [Theory]
        [InlineData(null, "cs", "sd", "sc")]
        [InlineData("ci", null, "sd", "sc")]
        [InlineData("ci", "cs", null, null)]
        //         ("ci", "cs", null, "sc") // Custom scopes have precedence
        //         ("ci", "cs", "sd", null) // Default scopes are fallback
        //         ("ci", "cs", "sd", "sc") // Custom scopes have precedence
        public async Task ThrowsForMissingCustomConfig(
            string? clientId,
            string? clientSecret,
            string? scopesDefault,
            string? scopesCustom)
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService(Options.Create(new SlackOptions()
                {
                    AppId = "A012345",
                    ClientId = "ci-config",
                    ClientSecret = "cs-config",
                    RequiredScopes = scopesDefault,
                    CustomAppScopes = scopesCustom,
                }))
                .Build();
            var org = env.TestData.Organization;
            var app = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(app.Enabled); // Ignored
            app.ExternalId = "A98765"; // Not installed
            await env.Integrations.SaveSettingsAsync(app,
                new SlackAppSettings
                {
                    Credentials = new SlackCredentials
                    {
                        ClientId = clientId,
                        ClientSecret = env.Secret(clientSecret),
                        SigningSecret = env.Secret("ignored"),
                    },
                });

            var authenticator = env.Activate<SlackAuthenticator>();

            var ex = await Assert.ThrowsAsync<UnreachableException>(() =>
                authenticator.GetCredentialsAsync(org, OAuthAction.InstallCustom)
            );
        }

        [Theory]
        [InlineData(null)] // New install
        [InlineData("A012345")] // Reinstall
        public async Task ReturnsClientIdAndSecretFromConfig(string botAppId)
        {
            var env = TestEnvironmentBuilder.Create()
                .Configure<SlackOptions>(o => {
                    o.AppId = "A012345";
                    o.ClientId = "ci-config";
                    o.ClientSecret = "cs-config";
                    o.RequiredScopes = "required,scopes";
                    o.CustomAppScopes = "custom,scopes";
                })
                .Build();
            var org = env.TestData.Organization;
            org.BotAppId = botAppId;

            var authenticator = env.Activate<SlackAuthenticator>();

            var credentials = await authenticator.GetCredentialsAsync(org, OAuthAction.Install);

            Assert.Equal("ci-config", credentials.ClientId);
            Assert.Equal("cs-config", credentials.ClientSecret);
            Assert.Equal("required,scopes", credentials.Scopes);
        }

        [Fact]
        public async Task ReturnsClientIdAndSecretFromConfigWhenIntegrationNotInstalled()
        {
            var env = TestEnvironmentBuilder.Create()
                .Configure<SlackOptions>(o => {
                    o.AppId = "A012345";
                    o.ClientId = "ci-config";
                    o.ClientSecret = "cs-config";
                    o.RequiredScopes = "required,scopes";
                    o.CustomAppScopes = "custom,scopes";
                })
                .Build();
            var org = env.TestData.Organization;
            var app = await env.Integrations.EnableAsync(org, IntegrationType.SlackApp, env.TestData.Member);
            Assert.True(app.Enabled); // Ignored
            app.ExternalId = "A98765"; // Not installed
            await env.Integrations.SaveSettingsAsync(app,
                new SlackAppSettings
                {
                    Credentials = new SlackCredentials()
                    {
                        ClientId = "ci",
                        ClientSecret = env.Secret("cs"),
                        SigningSecret = env.Secret("ignored"),
                    },
                });

            var authenticator = env.Activate<SlackAuthenticator>();

            var credentials = await authenticator.GetCredentialsAsync(org, OAuthAction.Install);

            Assert.Equal("ci-config", credentials.ClientId);
            Assert.Equal("cs-config", credentials.ClientSecret);
            Assert.Equal("required,scopes", credentials.Scopes);
        }

        [Theory]
        [InlineData(null, "required,scopes")]
        [InlineData("custom,scopes", "custom,scopes")]
        public async Task ReturnsClientIdAndSecretFromCustomConfigWhenIntegrationInstalled(
            string? customScopes,
            string expectedScopes)
        {
            var env = TestEnvironmentBuilder.Create()
                .Configure<SlackOptions>(o => {
                    o.AppId = "A012345";
                    o.ClientId = "ci-config";
                    o.ClientSecret = "cs-config";
                    o.RequiredScopes = "required,scopes";
                    o.CustomAppScopes = customScopes;
                })
                .Build();
            var org = env.TestData.Organization;
            org.BotAppId = "A98765";
            var app = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(app.Enabled); // Ignored
            app.ExternalId = org.BotAppId; // Installed
            await env.Integrations.SaveSettingsAsync(app, new SlackAppSettings
            {
                Credentials = new SlackCredentials
                {
                    ClientId = "ci",
                    ClientSecret = env.Secret("cs"),
                    SigningSecret = env.Secret("ignored"),
                },
            });

            var authenticator = env.Activate<SlackAuthenticator>();

            var credentials = await authenticator.GetCredentialsAsync(org, OAuthAction.Install);

            Assert.Equal("ci", credentials.ClientId);
            Assert.Equal("cs", credentials.ClientSecret);
            Assert.Equal(expectedScopes, credentials.Scopes);
        }

        [Theory]
        [InlineData(null, "required,scopes")]
        [InlineData("custom,scopes", "custom,scopes")]
        public async Task ReturnsClientIdAndSecretFromCustomConfig(
            string? customScopes,
            string expectedScopes)
        {
            var env = TestEnvironmentBuilder.Create()
                .Configure<SlackOptions>(o => {
                    o.AppId = "A012345";
                    o.ClientId = "ci-config";
                    o.ClientSecret = "cs-config";
                    o.RequiredScopes = "required,scopes";
                    o.CustomAppScopes = customScopes;
                })
                .Build();
            var org = env.TestData.Organization;
            var app = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(app.Enabled); // Ignored
            app.ExternalId = "A98765"; // Not installed
            await env.Integrations.SaveSettingsAsync(app,
                new SlackAppSettings
                {
                    Credentials = new SlackCredentials
                    {
                        ClientId = "ci",
                        ClientSecret = env.Secret("cs"),
                        SigningSecret = env.Secret("ignored"),
                    },
                });

            var authenticator = env.Activate<SlackAuthenticator>();

            var credentials = await authenticator.GetCredentialsAsync(org, OAuthAction.InstallCustom);

            Assert.Equal("ci", credentials.ClientId);
            Assert.Equal("cs", credentials.ClientSecret);
            Assert.Equal(expectedScopes, credentials.Scopes);
        }
    }

    public class TheGetInstallUrlAsyncMethod
    {
#pragma warning disable IDE1006 // Leave me alone
        const string state = "state!";
#pragma warning restore IDE1006

        [Theory]
        [InlineData(null)] // New install
        [InlineData("A012345")] // Reinstall
        public async Task ReturnsUrlFromConfig(string botAppId)
        {
            var env = TestEnvironmentBuilder.Create()
                .Configure<SlackOptions>(o => {
                    o.ClientId = "ci";
                    o.ClientSecret = "cs";
                    o.RequiredScopes = "rs";
                    o.CustomAppScopes = "custom,scopes";
                })
                .Build();
            var org = env.TestData.Organization;
            org.BotAppId = botAppId;

            var authenticator = env.Activate<SlackAuthenticator>();

            var res = await authenticator.GetInstallUrlAsync(org, OAuthAction.Install, state);

            Assert.Equal(
                $"https://slack.com/oauth/v2/authorize?client_id=ci&scope=rs&redirect_uri=https://app.ab.bot/slack/installed&team={org.PlatformId}&state=state!",
                res
            );
        }

        [Fact]
        public async Task ReturnsUrlFromConfigWhenIntegrationNotInstalled()
        {
            var env = TestEnvironmentBuilder.Create()
                .Configure<SlackOptions>(o => {
                    o.AppId = "A012345";
                    o.ClientId = "ci-config";
                    o.ClientSecret = "cs-config";
                    o.RequiredScopes = "rs";
                    o.CustomAppScopes = "custom,scopes";
                })
                .Build();
            var org = env.TestData.Organization;
            var app = await env.Integrations.EnableAsync(org, IntegrationType.SlackApp, env.TestData.Member);
            Assert.True(app.Enabled); // Ignored
            app.ExternalId = "A98765"; // Not installed
            await env.Integrations.SaveSettingsAsync(app,
                new SlackAppSettings
                {
                    Credentials = new SlackCredentials
                    {
                        ClientId = "ci",
                        ClientSecret = env.Secret("cs"),
                        SigningSecret = env.Secret("ignored"),
                    },
                });

            var authenticator = env.Activate<SlackAuthenticator>();

            var res = await authenticator.GetInstallUrlAsync(org, OAuthAction.Install, state);

            Assert.Equal(
                $"https://slack.com/oauth/v2/authorize?client_id=ci-config&scope=rs&redirect_uri=https://app.ab.bot/slack/installed&team={org.PlatformId}&state=state!",
                res
            );
        }

        [Theory]
        [InlineData(null, "required,scopes")]
        [InlineData("custom,scopes", "custom,scopes")]
        public async Task ReturnsUrlFromCustomConfigWhenIntegrationInstalled(
            string? customScopes,
            string expectedScopes)
        {
            var env = TestEnvironmentBuilder.Create()
                .Configure<SlackOptions>(o => {
                    o.AppId = "A012345";
                    o.ClientId = "ci-config";
                    o.ClientSecret = "cs-config";
                    o.RequiredScopes = "required,scopes";
                    o.CustomAppScopes = customScopes;
                })
                .Build();
            var org = env.TestData.Organization;
            org.BotAppId = "A98765";
            var app = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(app.Enabled); // Ignored
            app.ExternalId = org.BotAppId; // Installed
            await env.Integrations.SaveSettingsAsync(app, new SlackAppSettings
            {
                Credentials = new SlackCredentials
                {
                    ClientId = "ci",
                    ClientSecret = env.Secret("cs"),
                    SigningSecret = env.Secret("ignored"),
                },
            });

            var authenticator = env.Activate<SlackAuthenticator>();

            var res = await authenticator.GetInstallUrlAsync(org, OAuthAction.Install, state);

            Assert.Equal(
                $"https://slack.com/oauth/v2/authorize?client_id=ci&scope={expectedScopes}&redirect_uri=https://app.ab.bot/slack/installed&team={org.PlatformId}&state=state!",
                res
            );
        }

        [Theory]
        [InlineData(null, "required,scopes")]
        [InlineData("custom,scopes", "custom,scopes")]
        public async Task ReturnsUrlFromCustomConfig(string? customScopes, string expectedScopes)
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService(Options.Create(new SlackOptions()
                {
                    AppId = "A012345",
                    ClientId = "ci-config",
                    ClientSecret = "cs-config",
                    RequiredScopes = "required,scopes",
                    CustomAppScopes = customScopes,
                }))
                .Build();
            var org = env.TestData.Organization;
            var app = await env.Integrations.EnsureIntegrationAsync(org, IntegrationType.SlackApp);
            Assert.False(app.Enabled); // Ignored
            app.ExternalId = "A98765"; // Not installed
            await env.Integrations.SaveSettingsAsync(app,
                new SlackAppSettings
                {
                    Credentials = new SlackCredentials
                    {
                        ClientId = "ci",
                        ClientSecret = env.Secret("cs"),
                        SigningSecret = env.Secret("ignored"),
                    },
                });

            var authenticator = env.Activate<SlackAuthenticator>();

            var res = await authenticator.GetInstallUrlAsync(org, OAuthAction.InstallCustom, state);

            Assert.Equal(
                $"https://slack.com/oauth/v2/authorize?client_id=ci&scope={expectedScopes}&redirect_uri=https://app.ab.bot/slack/installed&team={org.PlatformId}&state=state!",
                res
            );
        }
    }

    public class TheGetCorrelationValueMethod
    {
        // TODO: Extract from SlackControllerTests
    }

    public class TheTryValidateCorrelationValueMethod
    {
        // TODO: Extract from SlackControllerTests
    }
}
