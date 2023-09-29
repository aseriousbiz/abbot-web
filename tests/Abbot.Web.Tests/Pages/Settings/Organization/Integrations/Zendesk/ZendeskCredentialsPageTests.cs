using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Settings.Organization.Integrations.Zendesk;
using Serious.Cryptography;
using Xunit;

public class ZendeskCredentialsPageTests
{
    public class TheOnGetMethod : PageTestBase<CredentialsPage>
    {
        [Fact]
        public async Task RendersPageWithCurrentSubdomain()
        {
            var integration = await Env.Integrations.EnsureIntegrationAsync(Env.TestData.Organization, IntegrationType.Zendesk);
            var settings = new ZendeskSettings()
            {
                Subdomain = "foo",
            };

            await Env.Integrations.SaveSettingsAsync(integration, settings);
            var (page, _) = await InvokePageAsync<PageResult>(p => p.OnGet());

            Assert.Same(integration, page.Integration);
            Assert.Equal("foo", page.Settings?.Subdomain);
        }
    }

    public class TheOnPostMethod : PageTestBase<CredentialsPage>
    {
        [Fact]
        public async Task UpdatesSubdomain()
        {
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-old",
                });
            var (page, result) = await InvokePageAsync<RedirectToPageResult>(p => {
                p.Subdomain = "d3v-new";
                return p.OnPost();
            });

            await Env.ReloadAsync(integration);
            var settings = Env.Integrations.ReadSettings<ZendeskSettings>(integration);

            Assert.True(ModelState.IsValid);
            Assert.Equal("d3v-new", settings.Subdomain);
            Assert.Equal("/Settings/Organization/Integrations/Zendesk/Index", result.PageName);
        }

        [Fact]
        public async Task UninstallsAndDisablesIfInstalledAndSubdomainChanged()
        {
            Builder.Substitute<IZendeskInstaller>(out var zendeskInstaller);
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-old",
                    ApiToken = Env.Secret("access-token"),
                    WebhookId = "the-webhook",
                });

            await Env.LinkedIdentities.LinkIdentityAsync(Env.TestData.Organization,
                Env.TestData.Member,
                LinkedIdentityType.Zendesk,
                "https://d3v-old.zendesk.com",
                "Some Person",
                new ZendeskUserMetadata());

            var (identity, metadata) = await Env.LinkedIdentities.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                Env.TestData.Organization,
                Env.TestData.Member,
                LinkedIdentityType.Zendesk);
            Assert.NotNull(identity);
            Assert.NotNull(metadata);

            zendeskInstaller.UninstallFromZendeskAsync(Env.TestData.Organization,
                    Arg.Is<ZendeskSettings>(s => s.Subdomain == "d3v-old"))
                .Returns(call => {
                    var settings = call.Arg<ZendeskSettings>();
                    settings.ApiToken = null;
                    settings.WebhookId = null;
                    return Task.FromResult(settings);
                });

            var (page, result) = await InvokePageAsync<RedirectToPageResult>(p => {
                p.Subdomain = "d3v-new";
                return p.OnPost();
            });

            await Env.ReloadAsync(integration);
            var settings = Env.Integrations.ReadSettings<ZendeskSettings>(integration);

            Assert.True(ModelState.IsValid);
            Assert.Equal("d3v-new", settings.Subdomain);
            Assert.Null(settings.ApiToken);
            Assert.Null(settings.WebhookId);
            Assert.Equal("Zendesk integration was uninstalled. Please reinstall it to continue.", page.StatusMessage);
            Assert.Equal("/Settings/Organization/Integrations/Zendesk/Index", result.PageName);
            (identity, metadata) = await Env.LinkedIdentities.GetLinkedIdentityAsync<ZendeskUserMetadata>(
                Env.TestData.Organization,
                Env.TestData.Member,
                LinkedIdentityType.Zendesk);
            Assert.Null(identity);
            Assert.Null(metadata);
        }

        [Fact]
        public async Task RendersPageIfModelStateErrors()
        {
            var integration = await Env.Integrations.EnableAsync(Env.TestData.Organization, IntegrationType.Zendesk, Env.TestData.Member);
            await Env.Integrations.SaveSettingsAsync(integration,
                new ZendeskSettings()
                {
                    Subdomain = "d3v-old",
                });
            ModelState.AddModelError("Subdomain", "Subdomain is required");
            await InvokePageAsync<PageResult>(p => {
                p.Subdomain = "d3v-bad";
                return p.OnPost();
            });

            await Env.ReloadAsync(integration);
            var settings = Env.Integrations.ReadSettings<ZendeskSettings>(integration);

            Assert.False(ModelState.IsValid);
            Assert.Equal("d3v-old", settings.Subdomain);
        }
    }
}
