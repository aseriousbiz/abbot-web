using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Refit;
using Serious;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Integrations.Zendesk.Models;
using Serious.TestHelpers;
using Xunit;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeZendeskClient : IZendeskClient
{
    static readonly ZendeskUser Anonymous = new()
    {
        Role = "end-user"
    };

    readonly IdGenerator _idGenerator = new();
    readonly Dictionary<string, Exception> _toThrow = new();
    readonly Dictionary<long, ZendeskUser> _users = new();

    public ZendeskUser CurrentZendeskUser { get; set; } = Anonymous;

    public IDictionary<long, (ZendeskTicket Ticket, IList<Comment> Comments)> Tickets { get; } =
        new Dictionary<long, (ZendeskTicket Ticket, IList<Comment> Comments)>();

    public IDictionary<string, TriggerCategory> TriggerCategories { get; } = new Dictionary<string, TriggerCategory>();

    public IDictionary<string, Trigger> Triggers { get; } = new Dictionary<string, Trigger>();

    public IDictionary<string, Webhook> Webhooks { get; } = new Dictionary<string, Webhook>();

    public IDictionary<string, ZendeskOrganization> Organizations { get; } =
        new Dictionary<string, ZendeskOrganization>();

    public ZendeskSettings? Settings { get; set; }

    public Exception ThrowOn(string methodName, HttpStatusCode statusCode, HttpMethod method, string uri, object payload) =>
        ThrowOn(methodName, CreateApiException(statusCode, method, uri, payload));

    public Exception ThrowOn(string methodName, Exception? exception = null) =>
        _toThrow[methodName] = exception ?? new InvalidOperationException($"{methodName} should not be called!");

    public Task<UserMessage> GetCurrentUserAsync()
    {
        ThrowIfRequested();
        return Task.FromResult(new UserMessage
        {
            Body = CurrentZendeskUser
        });
    }

    public Task<UserMessage> GetUserAsync(long userId)
    {
        ThrowIfRequested();
        var result = _users.TryGetValue(userId, out var user)
            ? new UserMessage
            {
                Body = user
            }
            : new UserMessage
            {
                Body = Anonymous
            };

        return Task.FromResult(result);
    }

    public void AddUser(ZendeskUser zendeskUser)
    {
        _users.Add(zendeskUser.Id, zendeskUser);
    }

    public Task<UserMessage> CreateOrUpdateUserAsync(UserMessage user)
    {
        throw new NotImplementedException();
    }

    public Task<UserListMessage> SearchUsersAsync(string? query, string? externalId)
    {
        throw new NotImplementedException();
    }

    public async Task<TicketMessage> GetTicketAsync(long ticketId)
    {
        ThrowIfRequested();
        return Tickets.TryGetValue(ticketId, out var existing)
            ? new TicketMessage { Body = existing.Ticket }
            : throw CreateApiException(HttpStatusCode.NotFound,
                HttpMethod.Get,
                $"/api/v2/tickets/{ticketId}.json",
                new {
                    error = "RecordNotFound",
                    description = "Not found",
                });
    }

    public Task<TicketMessage> CreateTicketAsync(TicketMessage message)
    {
        ThrowIfRequested();
        if (message.Body is null)
        {
            throw new InvalidOperationException("Ticket must not be null");
        }

        var ticket = message.Body;
        ticket.Id = _idGenerator.GetId();
        ticket.Url = $"https://{Settings.Subdomain}.zendesk.com/api/v2/tickets/{ticket.Id}.json";
        var comments = new List<Comment>()
        {
            ticket.Comment.Require()
        };

        Tickets[ticket.Id] = (ticket, comments);

        return Task.FromResult(new TicketMessage()
        {
            Body = ticket
        });
    }

    public async Task<TicketMessage> UpdateTicketAsync(long ticketId, TicketMessage ticket)
    {
        ThrowIfRequested();
        if (!Tickets.TryGetValue(ticketId, out var existing))
        {
            throw CreateApiException(HttpStatusCode.NotFound,
                HttpMethod.Put,
                $"/api/v2/tickets/{ticketId}.json",
                new {
                    error = "RecordNotFound",
                    description = "Not Found",
                });
        }

        // Updates need to provide existing values
        Assert.NotNull(ticket.Body);
        Assert.Same(existing.Ticket, ticket.Body);
        ticket.Body.Status ??= existing.Ticket?.Status;

        // If there's a comment field, add it to the comments list
        if (ticket.Body.Comment is not null)
        {
            existing.Comments.Add(ticket.Body.Comment);
            ticket.Body.Comment = null;
        }

        Tickets[ticketId] = existing with
        {
            Ticket = ticket.Body
        };

        return new()
        {
            Body = ticket.Body
        };
    }

    public Task<CommentListMessage> ListTicketCommentsAsync(long ticketId, int pageSize, string? after)
    {
        ThrowIfRequested();
        return Task.FromResult(new CommentListMessage
        {
            Body = Array.Empty<Comment>(),
            Meta = new PaginationMetadata(),
        });
    }

    public async Task<TriggerCategoryMessage> GetTriggerCategoryAsync(string triggerCategoryId)
    {
        ThrowIfRequested();
        if (TriggerCategories.TryGetValue(triggerCategoryId, out var cat))
        {
            return new TriggerCategoryMessage()
            {
                Body = cat
            };
        }

        throw CreateApiException(HttpStatusCode.NotFound,
            HttpMethod.Get,
            $"/api/v2/trigger_categories/{triggerCategoryId}.json",
            new {
                errors = new ApiError[]
                {
                    new()
                    {
                        Code = "TriggerCategoryNotFound"
                    }
                }
            });
    }

    public Task<TriggerCategoryMessage> CreateTriggerCategoryAsync(TriggerCategoryMessage message)
    {
        ThrowIfRequested();
        if (message.Body is null)
        {
            throw new InvalidOperationException("TriggerCategory must not be null");
        }

        var category = message.Body;
        category.Id = $"cat_{_idGenerator.GetId()}";
        TriggerCategories[category.Id] = category;

        return Task.FromResult(new TriggerCategoryMessage()
        {
            Body = category
        });
    }

    public async Task<TriggerMessage> GetTriggerAsync(string triggerId)
    {
        ThrowIfRequested();
        if (Triggers.TryGetValue(triggerId, out var trigger))
        {
            return new TriggerMessage()
            {
                Body = trigger
            };
        }

        throw CreateApiException(HttpStatusCode.NotFound,
            HttpMethod.Get,
            $"/api/v2/triggers/{triggerId}.json",
            new {
                error = "RecordNotFound",
                description = "Trigger not found",
            });
    }

    public Task<TriggerMessage> CreateTriggerAsync(TriggerMessage message)
    {
        ThrowIfRequested();
        if (message.Body is null)
        {
            throw new InvalidOperationException("Trigger must not be null");
        }

        var trigger = message.Body;
        trigger.Id = _idGenerator.GetId();
        Triggers[$"{trigger.Id}"] = trigger;

        return Task.FromResult(new TriggerMessage()
        {
            Body = trigger
        });
    }

    public Task UpdateTriggerAsync(string triggerId, TriggerMessage message)
    {
        ThrowIfRequested();
        if (message.Body is null)
        {
            throw new InvalidOperationException("Trigger must not be null");
        }

        message.Body.Id = int.Parse(triggerId);
        Triggers[triggerId] = message.Body;
        return Task.CompletedTask;
    }

    public Task<WebhookMessage> CreateWebhookAsync(WebhookMessage message)
    {
        ThrowIfRequested();
        if (message.Body is null)
        {
            throw new InvalidOperationException("Webhook must not be null");
        }

        var webhook = message.Body;
        webhook.Id = $"webhook_{_idGenerator.GetId()}";
        Webhooks[webhook.Id] = webhook;

        return Task.FromResult(new WebhookMessage()
        {
            Body = webhook
        });
    }

    public Task UpdateWebhookAsync(string webhookId, WebhookMessage message)
    {
        ThrowIfRequested();
        if (message.Body is null)
        {
            throw new InvalidOperationException("Webhook must not be null");
        }

        message.Body.Id = webhookId;
        Webhooks[webhookId] = message.Body;
        return Task.CompletedTask;
    }

    public async Task<WebhookMessage> GetWebhookAsync(string webhookId)
    {
        ThrowIfRequested();
        if (Webhooks.TryGetValue(webhookId, out var webhook))
        {
            return new WebhookMessage()
            {
                Body = webhook
            };
        }

        throw CreateApiException(HttpStatusCode.NotFound,
            HttpMethod.Get,
            $"/api/v2/webhooks/{webhookId}",
            new {
                errors = new ApiError[]
                {
                    new()
                    {
                        Code = "WebhookNotFound"
                    }
                }
            });
    }

    public async Task DeleteWebhookAsync(string webhookId)
    {
        ThrowIfRequested();
        if (Webhooks.Remove(webhookId))
        {
            return;
        }

        throw CreateApiException(HttpStatusCode.NotFound,
            HttpMethod.Delete,
            $"/api/v2/webhooks/{webhookId}",
            new {
                errors = new ApiError[]
                {
                    new()
                    {
                        Code = "WebhookNotFound"
                    }
                }
            });
    }

    public async Task DeleteTriggerAsync(string triggerId)
    {
        ThrowIfRequested();
        if (Triggers.Remove(triggerId))
        {
            return;
        }

        throw CreateApiException(HttpStatusCode.NotFound,
            HttpMethod.Delete,
            $"/api/v2/triggers/{triggerId}.json",
            new {
                error = "RecordNotFound",
                description = "Trigger not found",
            });
    }

    public async Task DeleteTriggerCategoryAsync(string triggerCategoryId)
    {
        ThrowIfRequested();
        if (TriggerCategories.Remove(triggerCategoryId))
        {
            return;
        }

        throw CreateApiException(HttpStatusCode.NotFound,
            HttpMethod.Delete,
            $"/api/v2/trigger_categories/{triggerCategoryId}.json",
            new {
                errors = new ApiError[]
                {
                    new()
                    {
                        Code = "TriggerCategoryNotFound"
                    }
                }
            });
    }

    public Task<OrganizationListMessage> AutocompleteOrganizationsAsync(string name)
    {
        ThrowIfRequested();
        return Task.FromResult(new OrganizationListMessage()
        {
            Body = Organizations
                .Where(p => p.Key.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value)
                .ToList()
        });
    }

    public Task CreateOrganizationMembershipAsync(OrganizationMembershipMessage membership)
    {
        throw new NotImplementedException();
    }

    [MemberNotNull(nameof(Settings))]
    void ThrowIfRequested([CallerMemberName] string? methodName = null)
    {
        Assert.NotNull(Settings);
        if (methodName is { Length: > 0 } && _toThrow.TryGetValue(methodName, out var exception))
        {
            throw exception;
        }
    }

    static ApiException CreateApiException(
        HttpStatusCode statusCode,
        HttpMethod method,
        string uri,
        object payload) => RefitTestHelpers
            .CreateApiException(
                statusCode,
                method,
                uri,
                payload);
}
