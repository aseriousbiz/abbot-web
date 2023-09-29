using System;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NuGet.Frameworks;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;
using Xunit;

public class IntegrationRepositoryTests
{
    public class TheCreateIntegrationAsyncMethod
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatesIntegrationEvenIfOneExists(bool enabled)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var type = IntegrationType.Zendesk;

            var repo = env.Activate<IntegrationRepository>();

            var integration1 = await repo.CreateIntegrationAsync(organization, type, enabled);

            Assert.Equal(1, integration1.Id);
            Assert.Equal(organization.Id, integration1.OrganizationId);
            Assert.Equal(type, integration1.Type);
            Assert.Equal(enabled, integration1.Enabled);

            var integration2 = await repo.CreateIntegrationAsync(organization, type, !enabled);

            Assert.Equal(2, integration2.Id);
            Assert.Equal(organization.Id, integration2.OrganizationId);
            Assert.Equal(type, integration2.Type);
            Assert.Equal(!enabled, integration2.Enabled);
        }
    }

    public class TheEnsureIntegrationAsyncMethod
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReturnsExistingIntegrationIfOneExists(bool enabled)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var type = IntegrationType.Zendesk;

            // Create even if another Org has an Integration
            env.Db.Integrations.Add(new Integration
            {
                Organization = env.TestData.ForeignOrganization,
                Type = type,
                Settings = "{}",
            });

            // Create even if this Org has a different Integration type
            env.Db.Integrations.Add(new Integration
            {
                Organization = organization,
                Type = IntegrationType.GitHub,
                Settings = "{}",
            });

            var repo = env.Activate<IntegrationRepository>();

            var integration1 = await repo.EnsureIntegrationAsync(organization, type, enabled);

            Assert.Equal(3, integration1.Id);
            Assert.Equal(organization.Id, integration1.OrganizationId);
            Assert.Equal(type, integration1.Type);
            Assert.Equal(enabled, integration1.Enabled);

            var integration2 = await repo.EnsureIntegrationAsync(organization, type, !enabled);

            Assert.Equal(integration1.Id, integration2.Id);
            Assert.Equal(organization.Id, integration2.OrganizationId);
            Assert.Equal(type, integration2.Type);
            Assert.Equal(enabled, integration2.Enabled);
        }
    }

    public class TheEnableAsyncMethod
    {
        [Theory]
        [InlineData(IntegrationType.None)]
        [InlineData(9999)]
        public async Task ThrowsForInvalidIntegrationType(IntegrationType integrationType)
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                repo.EnableAsync(env.TestData.Organization, integrationType, env.TestData.Member));

            Assert.Equal("type", ex.ParamName);
            Assert.Equal($"Invalid Integration Type: {integrationType} (Parameter 'type')", ex.Message);
        }

        [Fact]
        public async Task CreatesEnabledIntegrationRecordIfNoneExists()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.SlackApp, env.TestData.Member);

            var integration =
                await env.Db.Integrations.SingleAsync(i => i.OrganizationId == env.TestData.Organization.Id);

            Assert.Equal(IntegrationType.SlackApp, integration.Type);
            Assert.True(integration.Enabled);

            await env.AuditLog.AssertMostRecent<AdminAuditEvent>("Enabled Custom Slack App integration.");
        }

        [Fact]
        public async Task SetsEnabledToTrueIfDisabledIntegrationRecordExists()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await env.Db.Integrations.AddAsync(new Integration()
            {
                Organization = env.TestData.Organization,
                Type = IntegrationType.Zendesk,
                Enabled = false,
                Settings = "{}",
            });

            await env.Db.SaveChangesAsync();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var integration =
                await env.Db.Integrations.SingleAsync(i => i.OrganizationId == env.TestData.Organization.Id);

            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.True(integration.Enabled);

            await env.AuditLog.AssertMostRecent<AdminAuditEvent>("Enabled Zendesk integration.");
        }

        [Fact]
        public async Task CanEnableIfAlreadyEnabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await env.Db.Integrations.AddAsync(new Integration()
            {
                Organization = env.TestData.Organization,
                Type = IntegrationType.Zendesk,
                Enabled = true,
                Settings = "{}",
            });

            await env.Db.SaveChangesAsync();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var integration =
                await env.Db.Integrations.SingleAsync(i => i.OrganizationId == env.TestData.Organization.Id);

            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.True(integration.Enabled);

            await env.AuditLog.AssertNoRecent<AdminAuditEvent>();
        }
    }

    public class TheDisableAsyncMethod
    {
        [Theory]
        [InlineData(IntegrationType.None)]
        [InlineData(9999)]
        public async Task ThrowsForInvalidIntegrationType(IntegrationType integrationType)
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                repo.DisableAsync(env.TestData.Organization, integrationType, env.TestData.Member));

            Assert.Equal("type", ex.ParamName);
            Assert.Equal($"Invalid Integration Type: {integrationType} (Parameter 'type')", ex.Message);
        }

        [Fact]
        public async Task NoOpsIfNoIntegrationRecordExists()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.DisableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            Assert.Empty(await env.Db.Integrations.Where(i => i.OrganizationId == env.TestData.Organization.Id).ToListAsync());

            await env.AuditLog.AssertNoRecent<AdminAuditEvent>();
        }

        [Fact]
        public async Task SetsEnabledToFalseIfEnabledIntegrationRecordExists()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await env.Db.Integrations.AddAsync(new Integration()
            {
                Organization = env.TestData.Organization,
                Type = IntegrationType.SlackApp,
                Enabled = true,
                Settings = "{}",
            });

            await env.Db.SaveChangesAsync();

            await repo.DisableAsync(env.TestData.Organization, IntegrationType.SlackApp, env.TestData.Member);

            var integration =
                await env.Db.Integrations.SingleAsync(i => i.OrganizationId == env.TestData.Organization.Id);

            Assert.Equal(IntegrationType.SlackApp, integration.Type);
            Assert.False(integration.Enabled);

            await env.AuditLog.AssertMostRecent<AdminAuditEvent>("Disabled Custom Slack App integration.");
        }

        [Fact]
        public async Task CanDisableIfAlreadyDisabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await env.Db.Integrations.AddAsync(new Integration()
            {
                Organization = env.TestData.Organization,
                Type = IntegrationType.Zendesk,
                Enabled = false,
                Settings = "{}",
            });

            await env.Db.SaveChangesAsync();

            await repo.DisableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var integration =
                await env.Db.Integrations.SingleAsync(i => i.OrganizationId == env.TestData.Organization.Id);

            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.False(integration.Enabled);

            await env.AuditLog.AssertNoRecent<AdminAuditEvent>();
        }
    }

    public class TheGetIntegrationAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullIfNoIntegrationExists()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var integration = await repo.GetIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);

            Assert.Null(integration);
        }

        [Fact]
        public async Task ReturnsIntegrationIfEnabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var integration = await repo.GetIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);

            Assert.NotNull(integration);
            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.True(integration.Enabled);
        }

        [Fact]
        public async Task ReturnsIntegrationIfDisabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await repo.DisableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var integration = await repo.GetIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);

            Assert.NotNull(integration);
            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.False(integration.Enabled);
        }

        [Fact]
        public async Task ThrowsIfMultipleIntegrationsFound()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var type = IntegrationType.Zendesk;

            var repo = env.Activate<IntegrationRepository>();

            var i1 = await repo.CreateIntegrationAsync(organization, type);
            var i2 = await repo.CreateIntegrationAsync(organization, type);

            await Assert.ThrowsAsync<InvalidOperationException>(
                // Workaround for ambiguous call
                new Func<Task>(async () => {
                    await repo.GetIntegrationAsync(organization, type);
                })
            );
        }
    }

    public class TheGetIntegrationAsyncWithExternalIdMethod
    {
        [Fact]
        public async Task ReturnsNullNullIfNoIntegrationExists()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();
            var integrationConfig = await repo.EnableAsync(
                env.TestData.Organization,
                IntegrationType.HubSpot,
                env.TestData.Member);
            integrationConfig.ExternalId = "8675309";
            await env.Db.SaveChangesAsync();

            var (integration, settings) = await repo.GetIntegrationAsync<HubSpotSettings>(externalId: "DoesNotExist");

            Assert.Null(integration);
            Assert.Null(settings);
        }

        [Fact]
        public async Task ReturnsIntegrationIfEnabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();
            var integrationConfig = await repo.EnableAsync(
                env.TestData.Organization,
                IntegrationType.HubSpot,
                env.TestData.Member);
            integrationConfig.ExternalId = "8675309";
            await env.Db.SaveChangesAsync();

            var (integration, settings) = await repo.GetIntegrationAsync<HubSpotSettings>(externalId: "8675309");

            Assert.NotNull(integration);
            Assert.NotNull(settings);
            Assert.Equal(IntegrationType.HubSpot, integration.Type);
            Assert.True(integration.Enabled);
            Assert.Equal("8675309", integration.ExternalId);
        }

        [Fact]
        public async Task ReturnsIntegrationIfDisabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var integrationConfig = await repo.EnableAsync(env.TestData.Organization, IntegrationType.HubSpot, env.TestData.Member);
            integrationConfig.ExternalId = "8675309";
            await env.Db.SaveChangesAsync();
            await repo.DisableAsync(env.TestData.Organization, IntegrationType.HubSpot, env.TestData.Member);

            var (integration, settings) = await repo.GetIntegrationAsync<HubSpotSettings>(externalId: "8675309");

            Assert.NotNull(integration);
            Assert.NotNull(settings);
            Assert.Equal(IntegrationType.HubSpot, integration.Type);
            Assert.False(integration.Enabled);
        }

        [Fact]
        public async Task DeserializesHubSpotSettings()
        {
            var env = TestEnvironment.Create();
            var email = env.Secret("email@example.com");
            var token = env.Secret("this-is-a-token");
            var json = JsonConvert.SerializeObject(new {
                HubDomain = "hubdomain",
                Email = email.ProtectedValue,
                AccessToken = token.ProtectedValue,
            });
            var integration = await env.Integrations.EnsureIntegrationAsync(
                    env.TestData.Organization,
                    IntegrationType.HubSpot);
            integration.ExternalId = "8675309";
            await env.UpdateAsync(integration, i => i.Settings = json);
            var repo = env.Activate<IntegrationRepository>();

            var (_, settings) = await repo.GetIntegrationAsync<HubSpotSettings>(externalId: "8675309");

            Assert.NotNull(settings);
            Assert.Equal("hubdomain", settings.HubDomain);
            Assert.Equal("this-is-a-token", settings.AccessToken?.Reveal());
        }

        [Fact]
        public async Task DeserializesEmptyObjectAsHubSpotSettings()
        {
            var env = TestEnvironment.Create();
            var integration = await env.Integrations.EnsureIntegrationAsync(
                env.TestData.Organization,
                IntegrationType.HubSpot);
            integration.ExternalId = "8675309";
            await env.UpdateAsync(integration, i => i.Settings = "{}");
            var repo = env.Activate<IntegrationRepository>();

            var (_, settings) = await repo.GetIntegrationAsync<HubSpotSettings>(externalId: "8675309");

            Assert.NotNull(settings);
            Assert.Null(settings.HubDomain);
            Assert.Null(settings.AccessToken);
        }
    }

    public class TheGetIntegrationAsyncOfTSettingsMethod
    {
        [Fact]
        public async Task ReturnsNullNullIfNoIntegrationExists()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var (integration, settings) = await repo.GetIntegrationAsync<ZendeskSettings>(env.TestData.Organization);

            Assert.Null(integration);
            Assert.Null(settings);
        }

        [Fact]
        public async Task ReturnsIntegrationIfEnabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var (integration, settings) = await repo.GetIntegrationAsync<ZendeskSettings>(env.TestData.Organization);

            Assert.NotNull(integration);
            Assert.NotNull(settings);
            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.True(integration.Enabled);
        }

        [Fact]
        public async Task ReturnsIntegrationIfDisabled()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            await repo.DisableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var (integration, settings) = await repo.GetIntegrationAsync<ZendeskSettings>(env.TestData.Organization);

            Assert.NotNull(integration);
            Assert.NotNull(settings);
            Assert.Equal(IntegrationType.Zendesk, integration.Type);
            Assert.False(integration.Enabled);
        }

        [Fact]
        public async Task DeserializesZendeskSettings()
        {
            var env = TestEnvironment.Create();
            var email = env.Secret("email@example.com");
            var token = env.Secret("this-is-a-token");
            var json = JsonConvert.SerializeObject(new {
                Subdomain = "subdomain",
                Email = email.ProtectedValue,
                ApiToken = token.ProtectedValue,
            });

            await env.UpdateAsync(
                await env.Integrations.EnsureIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk),
                i => i.Settings = json);

            var (_, settings) =
                await env.Integrations.GetIntegrationAsync<ZendeskSettings>(env.TestData.Organization);

            Assert.NotNull(settings);
            Assert.Equal("subdomain", settings.Subdomain);
            Assert.Equal("this-is-a-token", settings.ApiToken?.Reveal());
        }

        [Fact]
        public async Task DeserializesEmptyObjectAsZendeskSettings()
        {
            var env = TestEnvironment.Create();

            await env.UpdateAsync(
                await env.Integrations.EnsureIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk),
                i => i.Settings = "{}");

            var (_, settings) =
                await env.Integrations.GetIntegrationAsync<ZendeskSettings>(env.TestData.Organization);

            Assert.NotNull(settings);
            Assert.Null(settings.Subdomain);
            Assert.Null(settings.ApiToken);
        }
    }

    public class TheGetIntegrationsAsyncMethod
    {
        [Fact]
        public async Task ReturnsEmptyListIfNoIntegrationsExist()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var integrations = await repo.GetIntegrationsAsync(env.TestData.Organization);

            Assert.NotNull(integrations);
            Assert.Empty(integrations);
        }

        [Fact]
        public async Task ReturnsAllIntegrationsIfAnyExist()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var integrations = await repo.GetIntegrationsAsync(env.TestData.Organization);

            Assert.NotNull(integrations);
            Assert.Equal(new[] { IntegrationType.Zendesk }, integrations.Select(i => i.Type).ToArray());
            Assert.Equal(new[] { env.TestData.Organization.Id }, integrations.Select(i => i.OrganizationId).ToArray());
        }
    }

    public class TheGetTicketingIntegrationByIdAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullIfIntegrationDoesNotExist()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var result = await repo.GetTicketingIntegrationByIdAsync(
                env.TestData.Organization,
                new Id<Integration>(999));

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullIfIntegrationIsNotTicketing()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var integration = await repo.CreateIntegrationAsync(
                env.TestData.Organization,
                IntegrationType.SlackApp);

            var result = await repo.GetTicketingIntegrationByIdAsync(
                env.TestData.Organization,
                integration);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullIfIntegrationDoesNotMatchOrganization()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var foreignIntegration = await repo.CreateIntegrationAsync(
                env.TestData.ForeignOrganization,
                IntegrationType.Ticketing);

            var result = await repo.GetTicketingIntegrationByIdAsync(
                env.TestData.Organization,
                foreignIntegration);

            Assert.Null(result);
        }

        [Theory]
        [InlineData(IntegrationType.Zendesk, typeof(ZendeskSettings))]
        [InlineData(IntegrationType.HubSpot, typeof(HubSpotSettings))]
        [InlineData(IntegrationType.GitHub, typeof(GitHubSettings))]
        [InlineData(IntegrationType.Ticketing, typeof(TicketingSettings))]
        public async Task ReturnsTicketingIntegration(IntegrationType integrationType, Type expectedSettingsType)
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var integration = await env.Integrations.CreateIntegrationAsync(
                env.TestData.Organization,
                integrationType);

            var result = await repo.GetTicketingIntegrationByIdAsync(
                env.TestData.Organization,
                integration);

            Assert.NotNull(result);
            Assert.Equal(integration, result.Integration);
            Assert.IsType(expectedSettingsType, result.Settings);
        }
    }

    public class TheGetTicketingIntegrationsAsyncMethod
    {
        [Fact]
        public async Task ReturnsEmptyListIfNoIntegrationsExist()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var integrations = await repo.GetTicketingIntegrationsAsync(env.TestData.Organization);

            Assert.NotNull(integrations);
            Assert.Empty(integrations);
        }

        [Fact]
        public async Task ReturnsEmptyListIfNoTicketingIntegrationsExist()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            await repo.EnableAsync(env.TestData.Organization, IntegrationType.SlackApp, env.TestData.Member);

            var integrations = await repo.GetTicketingIntegrationsAsync(env.TestData.Organization);

            Assert.NotNull(integrations);
            Assert.Empty(integrations);
        }

        [Fact]
        public async Task ReturnsAllIntegrationsIfAnyExist()
        {
            var env = TestEnvironment.Create();
            var repo = env.Activate<IntegrationRepository>();

            var integrations = new[]
            {
                await repo.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member),
                await repo.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.Ticketing, enabled: true),
                await repo.EnableAsync(env.TestData.Organization, IntegrationType.SlackApp, env.TestData.Member),
                await repo.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.HubSpot),
                await repo.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.GitHub),
                await repo.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.Ticketing),
            };

            var results = await repo.GetTicketingIntegrationsAsync(env.TestData.Organization);

            Assert.Equal(
                integrations.Where(i => i.Type is not IntegrationType.SlackApp),
                results.Select(ti => ti.Integration));

            Assert.Collection(results.Select(ti => ti.Settings),
                s => Assert.IsType<ZendeskSettings>(s),
                s => Assert.IsType<TicketingSettings>(s),
                // Skip SlackApp
                s => Assert.IsType<HubSpotSettings>(s),
                s => Assert.IsType<GitHubSettings>(s),
                s => Assert.IsType<TicketingSettings>(s));
        }
    }

    public class TheReadSettingsMethod
    {
        [Fact]
        public async Task DeserializesZendeskSettings()
        {
            var env = TestEnvironment.Create();
            var email = env.Secret("email@example.com");
            var token = env.Secret("this-is-a-token");
            var json = JsonConvert.SerializeObject(new {
                Subdomain = "subdomain",
                Email = email.ProtectedValue,
                ApiToken = token.ProtectedValue,
            });

            await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var integration =
                await env.Integrations.GetIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);
            Assert.NotNull(integration);
#pragma warning disable CS0618
            integration.Settings = json;
#pragma warning restore CS0618

            var settings = env.Integrations.ReadSettings<ZendeskSettings>(integration);

            Assert.Equal("subdomain", settings.Subdomain);
            Assert.Equal("this-is-a-token", settings.ApiToken?.Reveal());
        }

        [Fact]
        public async Task DeserializesEmptyObjectAsZendeskSettings()
        {
            var env = TestEnvironment.Create();

            await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var integration =
                await env.Integrations.GetIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);
            Assert.NotNull(integration);
            integration.Settings = "{}";

            var settings = env.Integrations.ReadSettings<ZendeskSettings>(integration);

            Assert.Null(settings.Subdomain);
            Assert.Null(settings.ApiToken);
        }
    }

    public class TheSaveSettingsAsyncMethod
    {
        [Fact]
        public async Task WritesSerializedSettingsToDatabase()
        {
            var env = TestEnvironment.Create();

            await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var integration =
                await env.Integrations.GetIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);
            Assert.NotNull(integration);

            var settings = new ZendeskSettings()
            {
                Subdomain = "subdomain",
                ApiToken = env.Secret("the-api-token"),
            };

            var result = await env.Integrations.SaveSettingsAsync(integration, settings);

            await env.ReloadAsync(integration);
            Assert.Same(settings, result);

            var loaded = env.Integrations.ReadSettings<ZendeskSettings>(integration);
            Assert.Equal("subdomain", loaded.Subdomain);
            Assert.Equal("the-api-token", loaded.ApiToken?.Reveal());
        }
    }
}
