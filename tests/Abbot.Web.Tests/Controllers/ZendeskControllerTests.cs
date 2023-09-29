using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Serious.Abbot.Controllers;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Security;
using Xunit;

namespace Abbot.Web.Tests.Controllers;

public static class ZendeskControllerTests
{
    static readonly string TestClientId = "zendesk-test-client-id";
    static readonly string TestClientSecret = "zendesk-test-client-secret";

    static void WithTestZendeskSettings<TEnvironment>(
        this TestEnvironmentBuilder<TEnvironment> self) where TEnvironment : class
    {
        self.Services.Configure<ZendeskOptions>(options => {
            options.ClientId = TestClientId;
            options.ClientSecret = TestClientSecret;
        });
    }

    public class TheWebhookMethod : ControllerTestBase<ZendeskController>
    {
        [Fact]
        public async Task ReturnsNotFoundIfOrganizationDoesNotExist()
        {
            var (_, result) = await InvokeControllerAsync<NotFoundObjectResult>(c => c.WebhookAsync(
                42,
                new WebhookPayload("https://example.com", 42)));
            Assert.Equal("Received Zendesk webhook for non-existent organization 42", result.Value);
        }

        [Fact]
        public async Task ReturnsBadRequestIfOrganizationHasNoZendeskCredentials()
        {
            var (_, result) = await InvokeControllerAsync<BadRequestObjectResult>(c => c.WebhookAsync(
                Env.TestData.Organization.Id,
                new WebhookPayload("https://example.com", 42)));
            Assert.Equal("Organization not configured for Zendesk integration", result.Value);
        }

        [Fact]
        public async Task ReturnsUnauthorizedIfTokenDoesNotMatchedSavedSettings()
        {
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "test",
                    ApiToken = Env.Secret("api-token"),
                    WebhookToken = "the-right-token",
                });
            HttpContext.Request.Headers["Authorization"] = "Bearer the-wrong-token";
            var (_, result) = await InvokeControllerAsync<UnauthorizedObjectResult>(c => c.WebhookAsync(
                Env.TestData.Organization.Id,
                new WebhookPayload("https://example.com", 42)));
            Assert.Equal("Bad webhook token", result.Value);
        }

        [Fact]
        public async Task ReturnsBadRequestIfTicketUrlIsMalformed()
        {
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "test",
                    ApiToken = Env.Secret("api-token"),
                    WebhookToken = "the-right-token",
                });
            HttpContext.Request.Headers["Authorization"] = "Bearer the-right-token";
            var (_, result) = await InvokeControllerAsync<BadRequestObjectResult>(c => c.WebhookAsync(
                Env.TestData.Organization.Id,
                new WebhookPayload("what even is this?!", 42)));
            Assert.Equal("Bad ticket URL in Zendesk webhook", result.Value);
        }

        [Fact]
        public async Task IgnoresRequestIfOrganizationDisabled()
        {
            Builder.Substitute<IZendeskToSlackImporter>(out var commentSyncer);

            Env.TestData.Organization.Enabled = false;
            await Env.Db.SaveChangesAsync();

            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = Env.Secret("api-token"),
                    WebhookToken = "the-right-token",
                });
            HttpContext.Request.Headers["Authorization"] = "Bearer the-right-token";
            var ticketLink = new ZendeskTicketLink("test", 42);
            var (_, result) = await InvokeControllerAsync<ContentResult>(c => c.WebhookAsync(
                Env.TestData.Organization.Id,
                new WebhookPayload("https://example.com/", 42)
                {
                    TicketUrl = ticketLink.ApiUrl.ToString(),
                }));
            Assert.Equal("No can do, boss. Organization is disabled.", result.Content);

            Assert.Empty(commentSyncer.ReceivedCalls());
        }

        [Fact]
        public async Task QueuesCommentSyncJobIfEverythingIsGood()
        {
            Builder.Substitute<IZendeskToSlackImporter>(out var commentSyncer);

            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings
                {
                    Subdomain = "test",
                    ApiToken = Env.Secret("api-token"),
                    WebhookToken = "the-right-token",
                });
            HttpContext.Request.Headers["Authorization"] = "Bearer the-right-token";
            var ticketLink = new ZendeskTicketLink("test", 42);
            var (_, result) = await InvokeControllerAsync<ContentResult>(c => c.WebhookAsync(
                Env.TestData.Organization.Id,
                new WebhookPayload("https://example.com/", 42)
                {
                    TicketUrl = ticketLink.ApiUrl.ToString(),
                }));
            Assert.Equal("I'll take it from here, boss!", result.Content);

            var call = Assert.Single(commentSyncer.ReceivedCalls());
            Assert.Equal(nameof(IZendeskToSlackImporter.QueueZendeskCommentImport), call.GetMethodInfo().Name);
            Assert.Equal(Env.TestData.Organization, call.GetArguments()[0]);
            Assert.Equal(ticketLink, call.GetArguments()[1]);
        }
    }

    public class TheCompleteAsyncMethod : ControllerTestBase<ZendeskController>
    {
        [Fact]
        public async Task FailsIfNoCodeProvided()
        {
            AuthenticateAs(Env.TestData.Member);
            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(c => c.CompleteAsync(null, "valid"));

            Assert.Equal("The Zendesk authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/Zendesk/Index", result.PageName);
        }

        [Fact]
        public async Task FailsIfStateInvalid()
        {
            Builder.Substitute<ICorrelationService>(out var correlationService);
            AuthenticateAs(Env.TestData.Member);
            Env.Clock.Freeze();

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                        c.HttpContext,
                        "invalid",
                        ZendeskController.CorrelationCookieName,
                        Env.Clock.UtcNow,
                        out _)
                    .Returns(false);

                return await c.CompleteAsync("abc", "invalid");
            });

            Assert.Equal("The Zendesk authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/Zendesk/Index", result.PageName);
        }

        [Fact]
        public async Task FailsIfAuthenticatedOrganizationDoesNotMatchState()
        {
            Builder.WithTestZendeskSettings();
            Builder.Substitute<ICorrelationService>(out var correlationService);
            AuthenticateAs(Env.TestData.Member);
            Env.Clock.Freeze();

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    ZendeskController.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(Env.Clock.UtcNow, Env.Clock.UtcNow.AddMinutes(15), OAuthAction.Install, Env.TestData.Organization.Id + 1);
                        return true;
                    });
                return await c.CompleteAsync("the-code", "valid");
            });

            Assert.Equal("The Zendesk authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/Zendesk/Index", result.PageName);
        }

        [Theory]
        [InlineData("end-user")]
        [InlineData("agent")]
        public async Task FailsIfUserIsNotAdmin(string returnedRole)
        {
            Builder.WithTestZendeskSettings();
            Builder.Substitute<IZendeskClientFactory>(out var clientFactory);
            Builder.Substitute<ICorrelationService>(out var correlationService);
            AuthenticateAs(Env.TestData.Member);
            Env.Clock.Freeze();

            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-test",
                });

            // This may not be the actual route, that's fine. This is for testing.
            Env.Router.MapVirtualPath("/zendesk/install/complete", new { action = "Complete", controller = "Zendesk" });

            var oauthClient = Substitute.For<IZendeskOAuthClient>();
            var apiClient = Substitute.For<IZendeskClient>();
            clientFactory.CreateOAuthClient("d3v-test").Returns(oauthClient);

            oauthClient.RedeemCodeAsync(
                "the-code",
                TestClientId,
                TestClientSecret,
                $"http://localhost/zendesk/install/complete",
                "read%20write")
                .Returns(new OAuthTokenMessage()
                {
                    AccessToken = "you-have-access",
                    Scope = "read write",
                    TokenType = "bearer",
                });

            ZendeskSettings? clientProvidedSettings = null;
            clientFactory.CreateClient(Arg.Do<ZendeskSettings>(s => clientProvidedSettings = s)).Returns(apiClient);
            apiClient.GetCurrentUserAsync().Returns(new UserMessage()
            {
                Body = new()
                {
                    Role = returnedRole
                }
            });

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    ZendeskController.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(Env.Clock.UtcNow, Env.Clock.UtcNow.AddMinutes(15), OAuthAction.Install, Env.TestData.Organization.Id);
                        return true;
                    });
                return await c.CompleteAsync("the-code", "valid");
            });

            Assert.Equal("you-have-access", clientProvidedSettings?.ApiToken?.Reveal());
            Assert.Equal("You must be a Zendesk admin to install Abbot.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/Zendesk/Index", result.PageName);

            await Env.ReloadAsync(integration);
            var updatedSettings = Env.Integrations.ReadSettings<ZendeskSettings>(integration);
            Assert.Null(updatedSettings.ApiToken);
            Assert.Null(updatedSettings.WebhookId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        public async Task CompletesInstallationIfCodeIsAuthentic(int fastForwardMinutes)
        {
            Builder.WithTestZendeskSettings();
            Builder.Substitute<IZendeskClientFactory>(out var clientFactory);
            Builder.Substitute<IZendeskInstaller>(out var installer);
            Builder.Substitute<ICorrelationService>(out var correlationService);
            AuthenticateAs(Env.TestData.Member);
            Env.Clock.Freeze();

            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-test",
                });

            // This may not be the actual route, that's fine. This is for testing.
            Env.Router.MapVirtualPath("/zendesk/install/complete", new { action = "Complete", controller = "Zendesk" });

            var client = Substitute.For<IZendeskOAuthClient>();
            var apiClient = Substitute.For<IZendeskClient>();
            clientFactory.CreateOAuthClient("d3v-test").Returns(client);

            client.RedeemCodeAsync(
                "the-code",
                TestClientId,
                TestClientSecret,
                $"http://localhost/zendesk/install/complete",
                "read%20write")
                .Returns(new OAuthTokenMessage()
                {
                    AccessToken = "you-have-access",
                    Scope = "read write",
                    TokenType = "bearer",
                });

            ZendeskSettings? clientProvidedSettings = null;
            clientFactory.CreateClient(Arg.Do<ZendeskSettings>(s => clientProvidedSettings = s)).Returns(apiClient);
            apiClient.GetCurrentUserAsync().Returns(new UserMessage()
            {
                Body = new()
                {
                    Role = "admin"
                }
            });

            ZendeskSettings? installerSettings;
            installer.InstallToZendeskAsync(Env.TestData.Organization, Arg.Any<ZendeskSettings>())
                .Returns(info => {
                    installerSettings = info.Arg<ZendeskSettings>();
                    installerSettings.WebhookId = "make sure you save this!";
                    return Task.FromResult(installerSettings);
                });

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    ZendeskController.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(Env.Clock.UtcNow, Env.Clock.UtcNow.AddMinutes(15), OAuthAction.Install, Env.TestData.Organization.Id);
                        return true;
                    });
                return await c.CompleteAsync("the-code", "valid");
            });

            Assert.Equal("you-have-access", clientProvidedSettings?.ApiToken?.Reveal());
            Assert.Equal("The Zendesk integration was successfully installed.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/Zendesk/Index", result.PageName);

            await Env.ReloadAsync(integration);
            var updatedSettings = Env.Integrations.ReadSettings<ZendeskSettings>(integration);
            Assert.Equal("you-have-access", updatedSettings.ApiToken?.Reveal());
            Assert.Equal("make sure you save this!", updatedSettings.WebhookId);
        }

        [Fact]
        public async Task CompletesLinkingIdentityIfCodeIsAuthentic()
        {
            Builder.WithTestZendeskSettings();
            Builder.Substitute<IZendeskClientFactory>(out var clientFactory);
            Builder.Substitute<IZendeskInstaller>(out var installer);
            Builder.Substitute<ICorrelationService>(out var correlationService);
            AuthenticateAs(Env.TestData.Member);
            Env.Clock.Freeze();

            var integration = await Env.Integrations.EnableAsync(
                Env.TestData.Organization,
                IntegrationType.Zendesk,
                Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-test",
                });

            // This may not be the actual route, that's fine. This is for testing.
            Env.Router.MapVirtualPath("/zendesk/install/complete", new { action = "Complete", controller = "Zendesk" });

            var client = Substitute.For<IZendeskOAuthClient>();
            var apiClient = Substitute.For<IZendeskClient>();
            clientFactory.CreateOAuthClient("d3v-test").Returns(client);

            client.RedeemCodeAsync(
                "the-code",
                TestClientId,
                TestClientSecret,
                $"http://localhost/zendesk/install/complete",
                "read%20write")
                .Returns(new OAuthTokenMessage()
                {
                    AccessToken = "you-have-access",
                    Scope = "read write",
                    TokenType = "bearer",
                });

            ZendeskSettings? clientProvidedSettings = null;
            clientFactory.CreateClient(Arg.Do<ZendeskSettings>(s => clientProvidedSettings = s)).Returns(apiClient);
            apiClient.GetCurrentUserAsync().Returns(new UserMessage()
            {
                Body = new()
                {
                    Role = "member",
                    Url = "https://d3v-test.zendesk.com/api/v2/users/123.json",
                    Name = "Jimmy Hendricks",
                    Email = "talking-guitar@example.com",
                }
            });

            ZendeskSettings? installerSettings;
            installer.InstallToZendeskAsync(Env.TestData.Organization, Arg.Any<ZendeskSettings>())
                .Returns(info => {
                    installerSettings = info.Arg<ZendeskSettings>();
                    installerSettings.WebhookId = "make sure you save this!";
                    return Task.FromResult(installerSettings);
                });

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    ZendeskController.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(Env.Clock.UtcNow, Env.Clock.UtcNow.AddMinutes(15), OAuthAction.Connect, Env.TestData.Organization.Id);
                        return true;
                    });
                return await c.CompleteAsync("the-code", "valid");
            });

            Assert.Equal("you-have-access", clientProvidedSettings?.ApiToken?.Reveal());
            Assert.Equal("Successfully linked your Zendesk account!", controller.StatusMessage);
            Assert.Equal("/Settings/Account/Index", result.PageName);

            var (linkedIdentity, metadata) = await Env.LinkedIdentities.GetLinkedIdentityAsync<ZendeskUserMetadata>(Env.TestData.Organization,
                Env.TestData.Member,
                LinkedIdentityType.Zendesk);
            Assert.NotNull(linkedIdentity);
            Assert.Equal("https://d3v-test.zendesk.com/api/v2/users/123.json", linkedIdentity.ExternalId);
            Assert.Equal("Jimmy Hendricks", linkedIdentity.ExternalName);
            Assert.NotNull(linkedIdentity.ExternalMetadata);

            Assert.NotNull(metadata);
            Assert.Equal("member", metadata.Role);
            Assert.Equal("d3v-test", metadata.Subdomain);
        }
    }

    public class TheInstallAsyncMethod : ControllerTestBase<ZendeskController>
    {
        [Fact]
        public async Task ReturnsErrorIfOrgIdMismatch()
        {
            AuthenticateAs(Env.TestData.Member);
            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(c => c.InstallAsync(Env.TestData.ForeignOrganization.Id));
            Assert.Equal("You are not authorized to perform this action", controller.StatusMessage);
            Assert.Equal("/Index", result.PageName);
        }

        [Theory]
        [InlineData(Roles.Administrator, "Zendesk integration is not properly configured.", "/Settings/Organization/Integrations/Zendesk")]
        [InlineData(Roles.Agent, "Zendesk integration is not properly configured. Contact an Administrator for help.", "/Index")]
        public async Task ReturnsErrorIfNoOAuthSettings(string role, string expectedMessage, string expectedPage)
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, role, Env.TestData.Abbot);
            AuthenticateAs(Env.TestData.Member);
            var (controller, result) =
                await InvokeControllerAsync<RedirectToPageResult>(c => c.InstallAsync(Env.TestData.Organization.Id));
            Assert.Equal(expectedMessage, controller.StatusMessage);
            Assert.Equal(expectedPage, result.PageName);
        }

        [Fact]
        public async Task RedirectsToZendeskIfSettingsConfigured()
        {
            Builder.WithTestZendeskSettings();
            Builder.Substitute<ICorrelationService>(out var correlationService);

            Env.Clock.Freeze();
            AuthenticateAs(Env.TestData.Member);
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-test",
                });

            // This may not be the actual route, that's fine. This is for testing.
            Env.Router.MapVirtualPath("/zendesk/install/complete", new { action = "Complete", controller = "Zendesk" });

            var (_, result) =
                await InvokeControllerAsync<RedirectResult>(c => {
                    correlationService.EncodeAndSetNonceCookie(
                        c.HttpContext,
                        ZendeskController.CorrelationCookieName,
                        Env.Clock.UtcNow,
                        Env.Clock.UtcNow.Add(ZendeskController.CorrelationExpiry),
                        OAuthAction.Install,
                        Env.TestData.Organization.Id)
                        .Returns("a state value");
                    return c.InstallAsync(Env.TestData.Organization.Id);
                });

            Assert.Equal("https://d3v-test.zendesk.com/oauth/authorizations/new?response_type=code&redirect_uri=http://localhost/zendesk/install/complete&client_id=zendesk-test-client-id&scope=read%20write&state=a state value", result.Url);
        }
    }
}
