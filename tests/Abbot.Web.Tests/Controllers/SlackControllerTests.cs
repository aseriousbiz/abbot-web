using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Serious;
using Serious.Abbot.Controllers;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Scripting;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Slack.AspNetCore;
using Serious.TestHelpers;
using Xunit;

public class SlackControllerTests : ControllerTestBase<SlackController>
{
    public SlackControllerTests() => Builder
        .Services.AddScoped<ISlackAuthenticator>(services =>
            new SlackAuthenticator(
                services.GetRequiredService<IIntegrationRepository>(),
                Options.Create(new SlackOptions()
                {
                    ClientId = "clientId",
                    ClientSecret = "clientSecret",
                    RequiredScopes = "mouthwash",
                }),
                services.GetRequiredService<IUrlGenerator>(),
                services.GetRequiredService<IClock>(),
                services.GetRequiredService<ICorrelationService>()))
        ;

    public class TheInstallMethod : SlackControllerTests
    {
        [Fact]
        public async Task RedirectsToSlackForInstallation()
        {
            Builder.Substitute<IBotInstaller>();
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();

            var (_, result) = await InvokeControllerAsync<RedirectResult>(c => {
                correlationService.EncodeAndSetNonceCookie(
                    c.HttpContext,
                    SlackAuthenticator.CorrelationCookieName,
                     Env.Clock.UtcNow,
                     Env.Clock.UtcNow.Add(SlackAuthenticator.CorrelationExpiry),
                    OAuthAction.Install,
                    Env.TestData.Organization.Id)
                    .Returns("a state value");
                return c.Install();
            });

            Assert.Equal($"https://slack.com/oauth/v2/authorize?client_id=clientId&scope=mouthwash&redirect_uri=https://app.ab.bot/slack/installed&team={Env.TestData.Organization.PlatformId}&state=a state value", result.Url);
        }
    }

    public class TheInstallCompleteAsyncMethod : SlackControllerTests
    {
        [Theory]
        [InlineData(OAuthAction.Install, "/Index")]
        [InlineData(OAuthAction.InstallCustom, "/Settings/Organization/Integrations/SlackApp/Index")]
        public async Task FailsIfNoCodeProvided(OAuthAction auth, string expectedPage)
        {
            Builder.Substitute<IBotInstaller>();
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    SlackAuthenticator.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(
                            Env.Clock.UtcNow,
                            Env.Clock.UtcNow.AddMinutes(15),
                            auth,
                            Env.TestData.ForeignOrganization.Id);
                        return true;
                    });
                return await c.InstallCompleteAsync(null, "valid");
            });

            Assert.Equal("The Slack authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal(expectedPage, result.PageName);
        }

        [Fact]
        public async Task FailsIfStateInvalid()
        {
            Builder.Substitute<IBotInstaller>();
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                        c.HttpContext,
                        "invalid",
                        SlackAuthenticator.CorrelationCookieName,
                        Env.Clock.UtcNow,
                        out _)
                    .Returns(false);

                return await c.InstallCompleteAsync("woop", "invalid");
            });

            Assert.Equal("The Slack authorization flow failed. Please try again.", controller.StatusMessage);
            // Cannot redirect to Integration because we can't get OAuthAction from CorrelationToken
            Assert.Equal("/Index", result.PageName);
        }

        [Theory]
        [InlineData(OAuthAction.Install, "/Index")]
        [InlineData(OAuthAction.InstallCustom, "/Settings/Organization/Integrations/SlackApp/Index")]
        public async Task FailsIfOrganizationIdDoesNotMatch(OAuthAction auth, string expectedPage)
        {
            Builder.Substitute<IBotInstaller>();
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    SlackAuthenticator.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(
                            Env.Clock.UtcNow,
                            Env.Clock.UtcNow.AddMinutes(15),
                            auth,
                            Env.TestData.ForeignOrganization.Id);
                        return true;
                    });
                return await c.InstallCompleteAsync("woop", "valid");
            });

            Assert.Equal("The Slack authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal(expectedPage, result.PageName);
        }

        [Theory]
        [InlineData(OAuthAction.Install, "/Account/Install/Complete", "TORG", null)]
        [InlineData(OAuthAction.InstallCustom, "/Settings/Organization/Integrations/SlackApp/Index", null, "Custom Slack App has been installed!")]
        public async Task CompletesInstallAndRedirectsIfCodeValid(OAuthAction auth, string expectedPage, string expectedTeamId, string expectedStatusMessage)
        {
            Builder.Substitute<IBotInstaller>(out var installer);
            Builder.Substitute<ISlackIntegration>(out var slackIntegration);
            Builder.Substitute<ISlackResolver>(out var resolver);
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();

            var installerMember = Env.TestData.Member;
            var installerPrincipal = AuthenticateAs(installerMember);

            var installEvent = new InstallEvent(
                "TORG",
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
                installerPrincipal);
            resolver.ResolveInstallEventFromOAuthResponseAsync("you are authentic", "clientId", "clientSecret", installerPrincipal)
                .Returns(installEvent);

            var installed = false;
            if (auth == OAuthAction.InstallCustom)
            {
                var slackApp = await Env.Integrations.EnsureIntegrationAsync(Env.TestData.Organization, IntegrationType.SlackApp);
                slackApp.ExternalId = installEvent.AppId;
                await Env.Integrations.SaveSettingsAsync(slackApp,
                    new SlackAppSettings
                    {
                        Credentials = new()
                        {
                            ClientId = "clientId",
                            ClientSecret = Env.Secret("clientSecret"),
                            SigningSecret = Env.Secret("signingSecret"),
                        },
                    });

                slackIntegration.InstallAsync(installEvent, Env.TestData.Member).Returns((_) => {
                    installed = true;
                    return Task.CompletedTask;
                });
            }
            else
            {
                installer.InstallBotAsync(installEvent).Returns((_) => {
                    installed = true;
                    return Task.CompletedTask;
                });
            }

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    SlackAuthenticator.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(
                            Env.Clock.UtcNow,
                            Env.Clock.UtcNow.AddMinutes(15),
                            auth,
                            Env.TestData.Organization.Id);
                        return true;
                    });
                return await c.InstallCompleteAsync("you are authentic", "valid");
            });

            Assert.Equal(expectedStatusMessage, controller.StatusMessage);
            Assert.Equal(expectedPage, result.PageName);
            Assert.Equal(expectedTeamId, result.RouteValues?["teamId"]);
            Assert.True(installed);
        }
    }
}
