using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.Logging;

namespace Serious.Abbot.Integrations.Zendesk;

public interface IZendeskOAuthClient
{
    [Post("/oauth/tokens?grant_type=authorization_code")]
    Task<OAuthTokenMessage> RedeemCodeAsync(
        string code,
        [AliasAs("client_id")] string clientId,
        [AliasAs("client_secret")] string clientSecret,
        [AliasAs("redirect_uri")] string redirectUri,
        string scope);
}

public interface IZendeskClient
{
    static readonly ILogger<IZendeskClient> Log = ApplicationLoggerFactory.CreateLogger<IZendeskClient>();

    [Get("/api/v2/users/me.json")]
    Task<UserMessage> GetCurrentUserAsync();

    [Get("/api/v2/users/{userId}.json")]
    Task<UserMessage> GetUserAsync(long userId);

    [Post("/api/v2/users/create_or_update.json")]
    Task<UserMessage> CreateOrUpdateUserAsync(UserMessage user);

    [Get("/api/v2/users/search.json")]
    Task<UserListMessage> SearchUsersAsync(string? query, string? externalId);

    [Post("/api/v2/tickets.json")]
    Task<TicketMessage> CreateTicketAsync(TicketMessage ticket);

    [Get("/api/v2/tickets/{ticketId}.json")]
    Task<TicketMessage> GetTicketAsync(long ticketId);

    async Task<ZendeskTicket?> SafelyGetTicketAsync(long ticketId)
    {
        try
        {
            var response = await GetTicketAsync(ticketId);
            return response.Body;
        }
        catch (Exception ex)
        {
            Log.RequestFailed(ex);
            return null;
        }
    }

    [Put("/api/v2/tickets/{ticketId}.json")]
    Task<TicketMessage> UpdateTicketAsync(long ticketId, TicketMessage ticket);

    [Get("/api/v2/tickets/{ticketId}/comments.json?page[size]={pageSize}&page[after]={after}")]
    Task<CommentListMessage> ListTicketCommentsAsync(long ticketId, int pageSize, string? after);

    [Get("/api/v2/trigger_categories/{triggerCategoryId}.json")]
    Task<TriggerCategoryMessage> GetTriggerCategoryAsync(string triggerCategoryId);

    [Post("/api/v2/trigger_categories.json")]
    Task<TriggerCategoryMessage> CreateTriggerCategoryAsync(TriggerCategoryMessage triggerCategory);

    [Get("/api/v2/triggers/{triggerId}.json")]
    Task<TriggerMessage> GetTriggerAsync(string triggerId);

    [Post("/api/v2/triggers.json")]
    Task<TriggerMessage> CreateTriggerAsync(TriggerMessage trigger);

    [Put("/api/v2/triggers/{triggerId}.json")]
    Task UpdateTriggerAsync(string triggerId, TriggerMessage trigger);

    // For some reason, the "webhooks" APIs don't use '.json' on the end of their URLs
    [Post("/api/v2/webhooks")]
    Task<WebhookMessage> CreateWebhookAsync(WebhookMessage webhook);

    [Put("/api/v2/webhooks/{webhookId}")]
    Task UpdateWebhookAsync(string webhookId, WebhookMessage webhook);

    [Get("/api/v2/webhooks/{webhookId}")]
    Task<WebhookMessage> GetWebhookAsync(string webhookId);

    [Delete("/api/v2/webhooks/{webhookId}")]
    Task DeleteWebhookAsync(string webhookId);

    [Delete("/api/v2/triggers/{triggerId}.json")]
    Task DeleteTriggerAsync(string triggerId);

    [Delete("/api/v2/trigger_categories/{triggerCategoryId}.json")]
    Task DeleteTriggerCategoryAsync(string triggerCategoryId);

    [Get("/api/v2/organizations/autocomplete.json?name={name}")]
    Task<OrganizationListMessage> AutocompleteOrganizationsAsync(string name);

    [Post("/api/v2/organization_memberships.json")]
    Task CreateOrganizationMembershipAsync(OrganizationMembershipMessage membership);
}

static partial class ZendeskClientLoggerExtensions
{
    [LoggerMessage(
        EventId = 4141,
        Level = LogLevel.Warning,
        Message = "Zendesk request failed.")]
    public static partial void RequestFailed(this ILogger logger, Exception ex);
}
