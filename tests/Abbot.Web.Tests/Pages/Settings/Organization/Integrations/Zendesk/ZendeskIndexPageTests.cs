using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire.Dashboard.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serious.Abbot;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Pages.Settings.Organization.Integrations.Zendesk;
using Serious.Cryptography;
using Xunit;

public class ZendeskIndexPageTests
{
    public class TheOnPostUninstallAsyncMethod : PageTestBase<IndexPage>
    {
        [Fact]
        public async Task ReturnsErrorIfIntegrationNotEnabled()
        {
            var (page, _) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostUninstallAsync());
            Assert.Equal("Zendesk integration is not enabled", page.StatusMessage);
        }

        [Fact]
        public async Task ReturnsErrorIfNoCredentialsConfigured()
        {
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            var (page, _) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostUninstallAsync());
            Assert.Equal("No Zendesk credentials configured", page.StatusMessage);
        }

        [Fact]
        public async Task ReturnsErrorIfUninstallationFails()
        {
            Builder.Substitute<IZendeskInstaller>(out var zdIntegration);
            zdIntegration.UninstallFromZendeskAsync(Arg.Any<Organization>(), Arg.Any<ZendeskSettings>())
                .Throws(new Exception("Barf"));

            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);

            var client = Env.ZendeskClientFactory.ClientFor("example-org");
            client.CurrentZendeskUser = new()
            {
                Role = "admin"
            };

            var integration =
                await Env.Integrations.GetIntegrationAsync(Env.TestData.Organization, IntegrationType.Zendesk);
            Assert.NotNull(integration);
            await Env.Integrations.SaveSettingsAsync(integration, new ZendeskSettings()
            {
                Subdomain = "example-org",
                ApiToken = Env.Secret("this-is-a-test-token"),
            });

            var (page, _) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostUninstallAsync());

            Assert.Equal($"Failed to uninstall Zendesk integration. Please try again, or contact '{WebConstants.SupportEmail}' for help.", page.StatusMessage);
        }

        [Fact]
        public async Task UninstallsAndDisablesZendeskIntegrationIfSettingsConfigured()
        {
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);

            var client = Env.ZendeskClientFactory.ClientFor("example-org");
            client.CurrentZendeskUser = new()
            {
                Role = "admin"
            };

            client.TriggerCategories["cat_99"] = new TriggerCategory();
            client.Triggers["99"] = new Trigger();
            client.Webhooks["webhook_99"] = new Webhook();

            var integration =
                await Env.Integrations.GetIntegrationAsync(Env.TestData.Organization, IntegrationType.Zendesk);
            Assert.NotNull(integration);
            await Env.Integrations.SaveSettingsAsync(integration, new ZendeskSettings()
            {
                Subdomain = "example-org",
                ApiToken = Env.Secret("this-is-a-test-token"),
                TriggerCategoryId = "cat_99",
                CommentPostedTriggerId = "99",
                WebhookId = "webhook_99",
            });

            var (page, _) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostUninstallAsync());

            await Env.ReloadAsync(integration);
            Assert.False(integration.Enabled);
            var settings = Env.Integrations.ReadSettings<ZendeskSettings>(integration);
            Assert.Empty(client.TriggerCategories);
            Assert.Null(settings.TriggerCategoryId);
            Assert.Empty(client.Triggers);
            Assert.Null(settings.CommentPostedTriggerId);
            Assert.Empty(client.Webhooks);
            Assert.Null(settings.WebhookId);
            Assert.Null(settings.WebhookToken);
        }
    }

    public class TheOnPostEnableAsyncMethod : PageTestBase<IndexPage>
    {
        [Fact]
        public async Task EnablesZendeskIntegrationIfNotEnabled()
        {
            var (page, result) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostEnableAsync());

            // When enabled after being disabled, the first thing you need to do is set credentials.
            Assert.Null(result.PageName);
            Assert.Equal("The Zendesk integration has been enabled.", page.StatusMessage);

            var integration =
                await Env.Integrations.GetIntegrationAsync(Env.TestData.Organization, IntegrationType.Zendesk);

            Assert.NotNull(integration);
            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.True(integration.Enabled);
        }

        [Fact]
        public async Task NoOpsIfAlreadyEnabled()
        {
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            var (page, result) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostEnableAsync());

            // We stay on the page if the integration is already enabled.
            Assert.Null(result.PageName);
            Assert.Equal("The Zendesk integration has been enabled.", page.StatusMessage);

            var integration =
                await Env.Integrations.GetIntegrationAsync(Env.TestData.Organization, IntegrationType.Zendesk);

            Assert.NotNull(integration);
            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.True(integration.Enabled);
        }
    }

    public class TheOnPostDisableAsyncMethod : PageTestBase<IndexPage>
    {
        [Fact]
        public async Task DisablesZendeskIntegrationIfEnabled()
        {
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);

            var (page, result) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostDisableAsync());

            Assert.Null(result.PageName);
            Assert.Equal("The Zendesk integration has been disabled.", page.StatusMessage);

            var integration =
                await Env.Integrations.GetIntegrationAsync(Env.TestData.Organization, IntegrationType.Zendesk);

            Assert.NotNull(integration);
            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.False(integration.Enabled);
        }

        [Fact]
        public async Task DisablingDoesNotAffectCredentials()
        {
            await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            var integration =
                await Env.Integrations.GetIntegrationAsync(Env.TestData.Organization, IntegrationType.Zendesk);
            Assert.NotNull(integration);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "subdomain",
                    ApiToken = Env.Secret("the-api-token"),
                });

            var (page, result) = await InvokePageAsync<RedirectToPageResult>(p => p.OnPostDisableAsync());

            Assert.Null(result.PageName);
            Assert.Equal("The Zendesk integration has been disabled.", page.StatusMessage);

            await Env.ReloadAsync(integration);
            var settings = Env.Integrations.ReadSettings<ZendeskSettings>(integration);

            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.False(integration.Enabled);
            Assert.Equal("subdomain", settings.Subdomain);
            Assert.Equal("the-api-token", settings.ApiToken?.Reveal());
        }
    }
}
