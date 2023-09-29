using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Logging;
using static Serious.Abbot.Integrations.HubSpot.HubSpotAssociationTypeId;

namespace Serious.Abbot.Integrations.HubSpot;

public static class HubSpotClientExtensions
{
    static readonly ILogger<IHubSpotClient> Log = ApplicationLoggerFactory.CreateLogger<IHubSpotClient>();

    // We're not absolutely sure these is true. This is just what we see in practice.

    /// <summary>
    /// Searches for a HubSpot ticket by the given property name and value.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="propertyName"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public static async Task<SearchResults?> SearchTicketsAsync(
        this IHubSpotClient client,
        string propertyName,
        string value) => await client.SearchAsync("tickets", propertyName, SearchOperator.ContainsToken, value);

    static async Task<SearchResults?> SearchAsync(
        this IHubSpotClient client,
        string searchType,
        string propertyName,
        SearchOperator searchOperator,
        string value)
    {
        var searchRequest = new SearchRequest(
            propertyName,
            searchOperator,
            value: value);

        try
        {
            return await client.SearchAsync(searchType, searchRequest);
        }
        catch (ApiException e)
        {
            Log.SearchFailure(e, searchType, propertyName, value, e.Content);
        }

        return null;
    }

    /// <summary>
    /// Retrieves a set of HubSpot ticket IDs associated with the given HubSpot conversation (thread) ID.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="threadId"></param>
    /// <returns></returns>
    public static async Task<IReadOnlyList<long>> GetTicketsAssociatedWithHubSpotConversation(
        this IHubSpotClient client,
        long threadId)
    {
        var response = await client.GetAssociationsAsync(
            fromObjectType: HubSpotObjectType.Conversation,
            threadId,
            toObjectType: HubSpotObjectType.Ticket);

        // It seems in practice, this only returns one, but we want to be defensive here.
        return response
            .Results
            .Where(r => r.AssociationTypes.Any(t => t.TypeId is (int)ConversationToTicket))
            .Select(r => r.ToObjectId)
            .ToList();
    }

    /// <summary>
    /// Retrieves a set of HubSpot conversation IDs associated with the given HubSpot ticket ID.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="ticketId">The Id of the ticket</param>
    /// <returns></returns>
    public static async Task<IReadOnlyList<long>> GetConversationsAssociatedWithHubSpotTicket(
        this IHubSpotClient client,
        long ticketId)
    {
        var response = await client.GetAssociationsAsync(
            fromObjectType: HubSpotObjectType.Ticket,
            ticketId,
            toObjectType: HubSpotObjectType.Conversation);

        // It seems in practice, this only returns one, but we want to be defensive here.
        return response
            .Results
            .Where(r => r.AssociationTypes.Any(t => t.TypeId is (int)TicketToConversation))
            .Select(r => r.ToObjectId)
            .ToList();
    }
}

static partial class HubSpotLinkerHubSpotClientLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Error searching for a HubSpot {SearchType} property {PropertyName} for the value `{Value}`. Error Content: {ErrorContent}")]
    public static partial void SearchFailure(
        this ILogger<IHubSpotClient> logger,
        Exception exception,
        string searchType,
        string propertyName,
        string value,
        string? errorContent);
}
