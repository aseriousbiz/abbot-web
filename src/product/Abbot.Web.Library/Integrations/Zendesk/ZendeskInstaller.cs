using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Integrations.Zendesk;

public interface IZendeskInstaller
{
    /// <summary>
    /// Installs the Zendesk resources (webhooks, triggers, etc.) necessary for the integration.
    /// This method returns an updated <see cref="ZendeskSettings"/> with any new settings.
    /// This <see cref="ZendeskSettings"/> should be saved with <see cref="IIntegrationRepository.SaveSettingsAsync{T}"/>.
    /// </summary>
    /// <param name="organization">The <see cref="Entities.Organization"/> you are testing credentials for.</param>
    /// <param name="settings">The <see cref="ZendeskSettings"/> containing credentials and any existing Zendesk resource IDs.</param>
    /// <returns>An updated <see cref="ZendeskSettings"/> with any new settings</returns>
    Task<ZendeskSettings> InstallToZendeskAsync(Organization organization, ZendeskSettings settings);

    /// <summary>
    /// Uninstalls any Zendesk resources (webhooks, triggers, etc.) the integration created.
    /// This method returns an updated <see cref="ZendeskSettings"/> with any new settings.
    /// This <see cref="ZendeskSettings"/> should be saved with <see cref="IIntegrationRepository.SaveSettingsAsync{T}"/>.
    /// </summary>
    /// <param name="organization">The <see cref="Entities.Organization"/> you are testing credentials for.</param>
    /// <param name="settings">The <see cref="ZendeskSettings"/> containing credentials and any existing Zendesk resource IDs.</param>
    /// <returns>An updated <see cref="ZendeskSettings"/> with any new settings</returns>
    Task<ZendeskSettings> UninstallFromZendeskAsync(Organization organization, ZendeskSettings settings);
}

public class ZendeskInstaller : IZendeskInstaller
{
    static readonly ILogger<ZendeskInstaller> Log = ApplicationLoggerFactory.CreateLogger<ZendeskInstaller>();
    readonly IUrlGenerator _urlGenerator;
    readonly IHostEnvironment _hostEnvironment;
    readonly IZendeskClientFactory _clientFactory;

    public ZendeskInstaller(IUrlGenerator urlGenerator, IHostEnvironment hostEnvironment, IZendeskClientFactory clientFactory)
    {
        _urlGenerator = urlGenerator;
        _hostEnvironment = hostEnvironment;
        _clientFactory = clientFactory;
    }

    public async Task<ZendeskSettings> InstallToZendeskAsync(Organization organization, ZendeskSettings settings)
    {
        var client = _clientFactory.CreateClient(settings);

        await InstallWebhookAsync(client, organization, settings);
        await InstallTriggerCategoryAsync(client, settings);
        await InstallTicketChangedTriggerAsync(client, settings);

        return settings;
    }

    public async Task<ZendeskSettings> UninstallFromZendeskAsync(Organization organization, ZendeskSettings settings)
    {
        var client = _clientFactory.CreateClient(settings);

        if (settings.CommentPostedTriggerId is { Length: > 0 })
        {
            try
            {
                await client.DeleteTriggerAsync(settings.CommentPostedTriggerId);
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            {
                // If it's not found, it's already been deleted.
            }

            settings.CommentPostedTriggerId = null;
        }

        if (settings.TriggerCategoryId is { Length: > 0 })
        {
            try
            {
                await client.DeleteTriggerCategoryAsync(settings.TriggerCategoryId);
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            {
                // If it's not found, it's already been deleted.
            }

            settings.TriggerCategoryId = null;
        }

        if (settings.WebhookId is { Length: > 0 })
        {
            try
            {
                await client.DeleteWebhookAsync(settings.WebhookId);
            }
            catch (ApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized)
            {
                // If it's not found, it's already been deleted.
            }

            settings.WebhookId = null;
        }

        settings.ApiToken = null;
        return settings;
    }

    static async Task<U?> GetExistingResourceAsync<T, U>(Func<Task<T>> getter)
        where T : ApiMessage<U>
    {
        try
        {
            var getResponse = await getter();
            return getResponse.Body;
        }
        catch (ApiException apiex) when (apiex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    async Task InstallWebhookAsync(IZendeskClient client, Organization organization, ZendeskSettings settings)
    {
        var zendeskEndpoint = _urlGenerator.ZendeskWebhookEndpoint(organization);

        // Generate a fresh authentication token.
        // We'll always be either creating or replacing a webhook so we can just generate a new token.
        settings.WebhookToken = TokenCreator.CreateMachineToken();

        var newWebhook = new WebhookMessage()
        {
            Body = new()
            {
                Authentication = new()
                {
                    AddPosition = "header",
                    Type = "bearer_token",
                    Data = new BearerTokenData()
                    {
                        Token = settings.WebhookToken
                    },
                },
                Endpoint = zendeskEndpoint.ToString(),
                HttpMethod = "POST",
                Name = GetName("Abbot Webhook"),
            }
        };

        var existingWebhook = settings.WebhookId is { Length: > 0 }
            ? await GetExistingResourceAsync<WebhookMessage, Webhook>(
                async () => await client.GetWebhookAsync(settings.WebhookId))
            : null;

        if (existingWebhook is null)
        {
            var response = await client.CreateWebhookAsync(newWebhook);
            settings.WebhookId = response.Body!.Id;
            Log.InstalledWebhook("Created", response.Body.Id, settings.Subdomain);
        }
        else
        {
            await client.UpdateWebhookAsync(existingWebhook.Id, newWebhook);
            Log.InstalledWebhook("Updated", existingWebhook.Id, settings.Subdomain);
        }
    }

    async Task InstallTriggerCategoryAsync(IZendeskClient client, ZendeskSettings settings)
    {
        var newTriggerCategory = new TriggerCategoryMessage()
        {
            Body = new()
            {
                Name = GetName("Abbot Triggers"),
            }
        };

        var existingCategory = settings.TriggerCategoryId is { Length: > 0 }
            ? await GetExistingResourceAsync<TriggerCategoryMessage, TriggerCategory>(
                async () => await client.GetTriggerCategoryAsync(settings.TriggerCategoryId))
            : null;

        if (existingCategory is null)
        {
            var response = await client.CreateTriggerCategoryAsync(newTriggerCategory);
            settings.TriggerCategoryId = response.Body!.Id;
            Log.InstalledTriggerCategory(response.Body!.Id, settings.Subdomain);
        }

        // No need to update the trigger category if it already exists.
    }

    async Task InstallTicketChangedTriggerAsync(IZendeskClient client, ZendeskSettings settings)
    {
        var newTrigger = new TriggerMessage
        {
            Body = new()
            {
                Title = GetName("Notify Abbot of ticket changes"),
                Description = "Notifies Abbot of ticket changes so that Abbot can forward comments to relevant Slack threads and update conversation state accordingly.",
                CategoryId = settings.TriggerCategoryId.Require(),
                Active = true,
                Actions = new List<Models.Action>
                {
                    new()
                    {
                        Field = "notification_webhook",
                        Value = new[] { settings.WebhookId, WebhookPayload.Template, }
                    }
                },
                Conditions = new()
                {
                    All = new List<Condition>
                    {
                        new()
                        {
                            Field = "update_type",
                            Operator = "is",
                            Value = "Change",
                        }
                    }
                }
            }
        };

        var existingTrigger = settings.CommentPostedTriggerId is { Length: > 0 }
            ? await GetExistingResourceAsync<TriggerMessage, Trigger>(
                async () => await client.GetTriggerAsync(settings.CommentPostedTriggerId))
            : null;

        if (existingTrigger is null)
        {
            var response = await client.CreateTriggerAsync(newTrigger);
            var id = $"{response.Body!.Id}";
            settings.CommentPostedTriggerId = id;
            Log.InstalledTrigger("Created", id, settings.Subdomain);
        }
        else
        {
            await client.UpdateTriggerAsync(settings.CommentPostedTriggerId!, newTrigger);
            Log.InstalledTrigger("Updated", settings.CommentPostedTriggerId!, settings.Subdomain);
        }
    }

    string GetName(string baseName)
    {
        return _hostEnvironment.IsProduction()
            ? baseName
            : $"{baseName} - {_urlGenerator.PublicHostName}";
    }
}

static partial class ZendeskInstallerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "{Action} Zendesk Webhook {WebhookId} in subdomain {Subdomain}")]
    public static partial void
        InstalledWebhook(this ILogger<ZendeskInstaller> logger, string action, string webhookId, string? subdomain);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Created Zendesk TriggerCategory {TriggerCategoryId} in subdomain {Subdomain}")]
    public static partial void InstalledTriggerCategory(this ILogger<ZendeskInstaller> logger, string triggerCategoryId,
        string? subdomain);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "{Action} Zendesk Trigger {TriggerId} in subdomain {Subdomain}")]
    public static partial void
        InstalledTrigger(
            this ILogger<ZendeskInstaller> logger,
            string action,
            string triggerId,
            string? subdomain);
}
