using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Serious.Abbot.Controllers;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Abbot.Security;
using Serious.Abbot.Services;
using Xunit;

namespace Abbot.Web.Tests.Controllers;

public class HubSpotControllerTests : ControllerTestBase<HubSpotController>
{
    public HubSpotControllerTests() => Builder.Services.Configure<HubSpotOptions>(options => {
        options.AppId = "app_id";
        options.ClientId = "client_id";
        options.ClientSecret = "client_secret";
        options.RequiredScopes = "mouthwash";
    });

    public class TheInstallMethod : HubSpotControllerTests
    {
        [Fact]
        public async Task RedirectsToHubSpotForInstallation()
        {
            Builder.Substitute<IBotInstaller>();
            Builder.Substitute<ICorrelationService>(out var correlationService);
            AuthenticateAs(Env.TestData.Member);
            Env.Clock.Freeze();
            Env.Router.MapVirtualPath("/hubspot/complete-install", new { action = "Complete", controller = "HubSpot" });
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.HubSpot, Env.TestData.Member);

            var (_, result) = await InvokeControllerAsync<RedirectResult>(c => {
                correlationService.EncodeAndSetNonceCookie(
                    c.HttpContext,
                    HubSpotController.CorrelationCookieName,
                     Env.Clock.UtcNow,
                     Env.Clock.UtcNow.Add(HubSpotController.CorrelationExpiry),
                    OAuthAction.Install,
                    Env.TestData.Organization.Id)
                    .Returns("a state value");

                return c.Install(Env.TestData.Organization.Id);
            });

            Assert.Equal("https://app.hubspot.com/oauth/authorize?response_type=code&redirect_uri=http://localhost/hubspot/complete-install&client_id=client_id&scope=mouthwash&state=a state value", result.Url);
        }
    }

    public class TheCompleteInstallAsyncMethod : HubSpotControllerTests
    {
        [Fact]
        public async Task FailsIfNoCodeProvided()
        {
            var (controller, result) =
                await InvokeControllerAsync<RedirectToPageResult>(c => c.CompleteAsync(null, "valid"));

            Assert.Equal("The HubSpot authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/HubSpot/Index", result.PageName);
        }

        [Fact]
        public async Task FailsIfStateInvalid()
        {
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.HubSpot, Env.TestData.Member);

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                        c.HttpContext,
                        "invalid",
                        ZendeskController.CorrelationCookieName,
                        Env.Clock.UtcNow,
                        out _)
                    .Returns(false);

                return await c.CompleteAsync("woop", "invalid");
            });

            Assert.Equal("The HubSpot authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/HubSpot/Index", result.PageName);
        }

        [Fact]
        public async Task FailsIfOrganizationIdDoesNotMatch()
        {
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.HubSpot, Env.TestData.Member);

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    ZendeskController.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = Env.TestData.ForeignOrganization.Id;
                        return true;
                    });
                return await c.CompleteAsync("woop", "invalid");
            });

            Assert.Equal("The HubSpot authorization flow failed. Please try again.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/HubSpot/Index", result.PageName);
        }

        [Fact]
        public async Task CompletesInstallAndRedirectsIfCodeValid()
        {
            Builder.Substitute<IHubSpotClientFactory>(out var clientFactory);
            Builder.Substitute<ICorrelationService>(out var correlationService);
            Env.Clock.Freeze();
            Env.Router.MapVirtualPath("/hubspot/complete-install", new { action = "Complete", controller = "HubSpot" });
            AuthenticateAs(Env.TestData.Member);

            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.HubSpot, Env.TestData.Member);

            var client = Substitute.For<IHubSpotOAuthClient>();
            clientFactory.CreateOAuthClient().Returns(client);

            OAuthRedeemRequest? providedRequest = null;
            client.RedeemCodeAsync(Arg.Any<OAuthRedeemRequest>())
                .Returns(call => {
                    providedRequest = call.Arg<OAuthRedeemRequest>();
                    return new OAuthRedeemResponse()
                    {
                        AccessToken = "access_token",
                        RefreshToken = "refresh_token",
                        ExpiresInSeconds = 30 * 60,
                    };
                });
            client.GetTokenInfoAsync("access_token")
                .Returns(_ => new OAuthTokenInfo()
                {
                    Scopes = new List<string>() { "mouthwash" },
                    HubId = 456,
                    AppId = 123,
                    HubDomain = "domain",
                    ExpiresInSeconds = 30 * 60,
                });

            var (controller, result) = await InvokeControllerAsync<RedirectToPageResult>(async c => {
                correlationService.TryValidate(
                    c.HttpContext,
                    "valid",
                    HubSpotController.CorrelationCookieName,
                    Env.Clock.UtcNow,
                    out Arg.Any<CorrelationToken?>())
                    .Returns(call => {
                        // Set the out param.
                        call[4] = new CorrelationToken(Env.Clock.UtcNow, Env.Clock.UtcNow.AddMinutes(15), OAuthAction.Install, Env.TestData.Organization.Id);
                        return true;
                    });
                return await c.CompleteAsync("you are authentic", "valid");
            });

            Assert.Equal("The HubSpot integration was successfully installed.", controller.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/HubSpot/Index", result.PageName);

            // Validate the new settings
            await Env.ReloadAsync(integration);
            var settings = Env.Integrations.ReadSettings<HubSpotSettings>(integration);
            Assert.NotNull(settings);
            Assert.Equal("access_token", settings.AccessToken?.Reveal());
            Assert.Equal("refresh_token", settings.RefreshToken?.Reveal());
            Assert.Equal(Env.Clock.UtcNow.AddMinutes(30), settings.AccessTokenExpiryUtc);
            Assert.Equal(new[] { "mouthwash" }, settings.ApprovedScopes);
            Assert.Equal("456", integration.ExternalId);
            Assert.Equal("domain", settings.HubDomain);

            // Validate the provided request
            var codeRequest = Assert.IsType<OAuthCodeRedeemRequest>(providedRequest);
            Assert.Equal("you are authentic", codeRequest.Code);
            Assert.Equal("client_id", codeRequest.ClientId);
            Assert.Equal("client_secret", codeRequest.ClientSecret);
            Assert.Equal("authorization_code", codeRequest.GrantType);
            Assert.Equal("http://localhost/hubspot/complete-install", codeRequest.RedirectUri);
        }
    }
}
