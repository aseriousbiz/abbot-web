using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Xunit;
using Action = Serious.Abbot.Integrations.Zendesk.Models.Action;

namespace Abbot.Web.Library.Tests.Integrations.Zendesk;

public class ZendeskInstallerTests
{
    static ZendeskSettings CreateTestSettings(TestEnvironment env)
    {
        return new ZendeskSettings()
        {
            Subdomain = "subdomain",
            ApiToken = env.Secret("a-secret-token"),
        };
    }

    public class TheInstallToZendeskAsyncMethod
    {
        [Theory]
        [InlineData("Abbot Webhook", "Production")]
        [InlineData("Abbot Webhook - test.ab.bot", "Flargus")]
        public async Task CreatesWebhookIfDoesNotExist(string expectedName, string environment)
        {
            var env = TestEnvironment.Create();
            env.HostEnvironment.EnvironmentName = environment;
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);

            settings = await integration.InstallToZendeskAsync(env.TestData.Organization, settings);

            var webhook = Assert.Single(client.Webhooks).Value;
            Assert.Equal($"https://app.ab.bot/api/integrations/zendesk/webhook/{env.TestData.Organization.Id}",
                webhook.Endpoint);

            Assert.Equal("POST", webhook.HttpMethod);
            Assert.Equal(expectedName, webhook.Name);
            Assert.Equal(settings.WebhookId, webhook.Id);
            Assert.Equal("header", webhook.Authentication.AddPosition);
            Assert.Equal("bearer_token", webhook.Authentication.Type);

            var tokenData = Assert.IsType<BearerTokenData>(webhook.Authentication.Data);
            Assert.Equal(settings.WebhookToken, tokenData.Token);
        }

        [Fact]
        public async Task ResetsWebhookTokenAndUpdatesIfAlreadyExists()
        {
            var env = TestEnvironment.Create();
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);

            var existingWebhook = new Webhook()
            {
                Id = "webhook_99",
                Name = "Old Name",
                Endpoint = "https://example.com",
                Authentication = new WebhookAuthentication()
                {
                    Data = new BearerTokenData()
                    {
                        Token = "old_token"
                    }
                },
            };

            var settings = CreateTestSettings(env);
            settings.WebhookToken = ((BearerTokenData)existingWebhook.Authentication.Data).Token;
            settings.WebhookId = existingWebhook.Id;
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            client.Webhooks[existingWebhook.Id] = existingWebhook;

            var integration = env.Activate<ZendeskInstaller>();

            settings = await integration.InstallToZendeskAsync(env.TestData.Organization, settings);

            var webhook = client.Webhooks[existingWebhook.Id];
            Assert.Equal($"https://app.ab.bot/api/integrations/zendesk/webhook/{env.TestData.Organization.Id}",
                webhook.Endpoint);
            Assert.Equal("POST", webhook.HttpMethod);
            Assert.Equal("Abbot Webhook - test.ab.bot", webhook.Name);
            Assert.Equal(settings.WebhookId, "webhook_99");
            Assert.Equal(webhook.Id, "webhook_99");
            Assert.Equal("header", webhook.Authentication.AddPosition);
            Assert.Equal("bearer_token", webhook.Authentication.Type);

            var tokenData = Assert.IsType<BearerTokenData>(webhook.Authentication.Data);
            Assert.NotEqual("old_token", tokenData.Token);
            Assert.Equal(settings.WebhookToken, tokenData.Token);
        }

        [Theory]
        [InlineData("Abbot Triggers", "Production")]
        [InlineData("Abbot Triggers - test.ab.bot", "Flargus")]
        public async Task CreatesTriggerCategoryIfDoesNotExist(string expectedName, string environment)
        {
            var env = TestEnvironment.Create();
            env.HostEnvironment.EnvironmentName = environment;
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);

            settings = await integration.InstallToZendeskAsync(env.TestData.Organization, settings);

            var category = Assert.Single(client.TriggerCategories).Value;
            Assert.Equal(expectedName, category.Name);
            Assert.Equal(settings.TriggerCategoryId, category.Id);
        }

        [Fact]
        public async Task DoesNotCreateTriggerCategoryIfAlreadyExists()
        {
            var env = TestEnvironment.Create();
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            settings.TriggerCategoryId = "cat_99";
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            client.TriggerCategories["cat_99"] = new TriggerCategory()
            {
                Id = "cat_99",
                Name = "Ye Olde Abbot Triggers",
            };

            client.ThrowOn(nameof(IZendeskClient.CreateTriggerCategoryAsync), new Exception("*thwap* No! Bad Code!"));

            settings = await integration.InstallToZendeskAsync(env.TestData.Organization, settings);

            var category = Assert.Single(client.TriggerCategories).Value;
            Assert.Equal("Ye Olde Abbot Triggers", category.Name);
            Assert.Equal("cat_99", category.Id);
            Assert.Equal("cat_99", settings.TriggerCategoryId);
        }

        [Theory]
        [InlineData("Notify Abbot of ticket changes", "Production")]
        [InlineData("Notify Abbot of ticket changes - test.ab.bot", "Flargus")]
        public async Task CreatesTriggerIfDoesNotExist(
            string expectedCommentTriggerName,
            string environment)
        {
            var env = TestEnvironment.Create();
            env.HostEnvironment.EnvironmentName = environment;
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            settings.WebhookId = "webhook_99";
            settings.TriggerCategoryId = "cat_99";
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);

            settings = await integration.InstallToZendeskAsync(env.TestData.Organization, settings);

            var trigger = Assert.Single(client.Triggers).Value;
            Assert.Equal(expectedCommentTriggerName, trigger.Title);
            Assert.Equal("Notifies Abbot of ticket changes so that Abbot can forward comments to relevant Slack threads and update conversation state accordingly.", trigger.Description);
            Assert.Equal(settings.TriggerCategoryId, trigger.CategoryId);
            Assert.True(trigger.Active);
            Assert.Collection(trigger.Actions,
                act => {
                    Assert.Equal("notification_webhook", act.Field);
                    Assert.Equal(new[]
                    {
                        settings.WebhookId,
                        WebhookPayload.Template,
                    }, act.Value);
                });
            Assert.Empty(trigger.Conditions.Any);
            var condition = Assert.Single(trigger.Conditions.All);
            Assert.Equal("update_type", condition.Field);
            Assert.Equal("is", condition.Operator);
            Assert.Equal("Change", condition.Value);
        }

        [Fact]
        public async Task UpdatesTriggerIfAlreadyExists()
        {
            var env = TestEnvironment.Create();
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            settings.WebhookId = "webhook_99";
            settings.TriggerCategoryId = "cat_99";
            settings.CommentPostedTriggerId = "99";
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            client.Triggers["99"] = new Trigger()
            {
                Id = 99,
                Title = "Who knows what this does",
                Description = "Yikes",
                Active = false,
                CategoryId = "cat_99999",
                Actions = new List<Action>()
                {
                    new()
                    {
                        Field = "panic",
                        Value = true,
                    }
                },
                Conditions = new()
                {
                    Any = new List<Condition>()
                    {
                        new()
                        {
                            Field = "should_panic",
                            Operator = "is",
                            Value = "false",
                        }
                    },
                    All = new List<Condition>()
                    {
                        new()
                        {
                            Field = "planet_danger",
                            Operator = "is_not",
                            Value = "mostly_harmless",
                        }
                    }
                }
            };

            settings = await integration.InstallToZendeskAsync(env.TestData.Organization, settings);

            var trigger = Assert.Single(client.Triggers).Value;
            Assert.Equal("Notify Abbot of ticket changes - test.ab.bot", trigger.Title);
            Assert.Equal(
                "Notifies Abbot of ticket changes so that Abbot can forward comments to relevant Slack threads and update conversation state accordingly.",
                trigger.Description);

            Assert.Equal(settings.TriggerCategoryId, trigger.CategoryId);
            Assert.True(trigger.Active);
            Assert.Collection(trigger.Actions,
                act => {
                    Assert.Equal("notification_webhook", act.Field);
                    Assert.Equal(new[]
                        {
                            settings.WebhookId, WebhookPayload.Template,
                        },
                        act.Value);
                });

            var condition = Assert.Single(trigger.Conditions.All);
            Assert.Equal("update_type", condition.Field);
            Assert.Equal("is", condition.Operator);
            Assert.Equal("Change", condition.Value);
        }
    }

    public class TheUninstallFromZendeskAsyncMethod
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public async Task DeletesWebhookIfPresent(bool? present)
        {
            var env = TestEnvironment.Create();
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            settings.WebhookId = "webhook_99";
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            if (present is true)
            {
                client.Webhooks[settings.WebhookId] = new Webhook();
            }
            else if (present is null)
            {
                client.ThrowOn(
                    nameof(IZendeskClient.DeleteWebhookAsync),
                    await env.CreateApiExceptionAsync(HttpStatusCode.Unauthorized, HttpMethod.Delete));
            }

            settings = await integration.UninstallFromZendeskAsync(env.TestData.Organization, settings);

            Assert.Empty(client.Webhooks);
            Assert.Null(settings.WebhookId);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public async Task DeletesTriggerIfPresent(bool? present)
        {
            var env = TestEnvironment.Create();
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            settings.CommentPostedTriggerId = "99";
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            if (present is true)
            {
                client.Triggers[settings.CommentPostedTriggerId] = new Trigger();
            }
            else if (present is null)
            {
                client.ThrowOn(
                    nameof(IZendeskClient.DeleteTriggerAsync),
                    await env.CreateApiExceptionAsync(HttpStatusCode.Unauthorized, HttpMethod.Delete));
            }

            settings = await integration.UninstallFromZendeskAsync(env.TestData.Organization, settings);

            Assert.Empty(client.Triggers);
            Assert.Null(settings.CommentPostedTriggerId);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [InlineData(null)]
        public async Task DeletesTriggerCategoryIfPresent(bool? present)
        {
            var env = TestEnvironment.Create();
            var integrationInfo =
                await env.Integrations.EnableAsync(env.TestData.Organization, IntegrationType.Zendesk, env.TestData.Member);
            var settings = CreateTestSettings(env);
            settings.TriggerCategoryId = "cat_99";
            await env.Integrations.SaveSettingsAsync(integrationInfo, settings);
            var integration = env.Activate<ZendeskInstaller>();

            var client = env.ZendeskClientFactory.ClientFor(settings.Subdomain);
            if (present is true)
            {
                client.TriggerCategories[settings.TriggerCategoryId] = new TriggerCategory();
            }
            else if (present is null)
            {
                client.ThrowOn(
                    nameof(IZendeskClient.DeleteTriggerCategoryAsync),
                    await env.CreateApiExceptionAsync(HttpStatusCode.Unauthorized, HttpMethod.Delete));
            }

            settings = await integration.UninstallFromZendeskAsync(env.TestData.Organization, settings);

            Assert.Empty(client.TriggerCategories);
            Assert.Null(settings.TriggerCategoryId);
        }
    }
}
