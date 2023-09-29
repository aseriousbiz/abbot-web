using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Refit;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Logging;

namespace Serious.Abbot.Integrations.HubSpot;

public interface IHubSpotClient
{
    static readonly ILogger<IHubSpotClient> Log = ApplicationLoggerFactory.CreateLogger<IHubSpotClient>();

    public static readonly Uri ApiUrl = new("https://api.hubapi.com");

    [Get("/integrations/v1/me")]
    Task<HubSpotAccount> GetMeAsync();

    /// <summary>
    /// Gets a <see cref="HubSpotContact"/> by <paramref name="contactId"/>.
    /// </summary>
    /// <param name="contactId">The Contact ID.</param>
    /// <returns></returns>
    [Get("/crm/v3/objects/contacts/{contactId}")]
    Task<HubSpotContact> GetContactAsync(string contactId);

    /// <inheritdoc cref="GetContactAsync(string)"/>
    async Task<HubSpotContact?> SafelyGetContactAsync(string contactId)
    {
        try
        {
            var response = await GetContactAsync(contactId);
            return response;
        }
        catch (Exception ex)
        {
            Log.RequestFailed(ex);
            return null;
        }
    }

    /// <summary>
    /// Gets a <see cref="HubSpotTicket"/> by <paramref name="ticketId"/>.
    /// </summary>
    /// <param name="ticketId">The </param>
    /// <returns></returns>
    [Get("/crm/v3/objects/tickets/{ticketId}")]
    Task<HubSpotTicket> GetTicketAsync(string ticketId);

    /// <inheritdoc cref="GetTicketAsync(string)"/>
    async Task<HubSpotTicket?> SafelyGetTicketAsync(string ticketId)
    {
        try
        {
            var response = await GetTicketAsync(ticketId);
            return response;
        }
        catch (Exception ex)
        {
            Log.RequestFailed(ex);
            return null;
        }
    }

    [Patch("/crm/v3/objects/tickets/{ticketId}")]
    Task<HubSpotTicket> UpdateTicketAsync(string ticketId, CreateOrUpdateTicket update);

    /// <summary>
    /// Creates a <see cref="HubSpotTicket"/>.
    /// </summary>
    /// <remarks>
    /// <see href="https://developers.hubspot.com/docs/api/crm/tickets"/>.
    /// </remarks>
    /// <param name="ticket">The <see cref="CreateOrUpdateTicket"/> details.</param>
    [Post("/crm/v3/objects/tickets")]
    Task<HubSpotTicket> CreateTicketAsync(CreateOrUpdateTicket ticket);

    /// <summary>
    /// Creates a <see cref="TimelineEvent"/> associated with a <see cref="HubSpotTicket"/>.
    /// </summary>
    /// <remarks>
    /// <see href="https://developers.hubspot.com/docs/api/crm/timeline"/>
    /// </remarks>
    /// <param name="evt">The <see cref="TimelineEvent"/> to add to the time line.</param>
    [Post("/crm/v3/timeline/events")]
    Task<TimelineEvent> CreateTimelineEventAsync(TimelineEvent evt);

    /// <summary>
    /// Retrieves the pipelines for a ticket.
    /// </summary>
    /// <remarks>
    /// In HubSpot, a pipeline is where deal stages or or ticket statuses are set.
    /// <see href="https://developers.hubspot.com/docs/api/crm/pipelines"/>
    /// </remarks>
    [Get("/crm/v3/pipelines/tickets")]
    Task<ListResult<TicketPipeline>> GetTicketPipelinesAsync();

    /// <summary>
    /// Search endpoints to filter, sort, and search objects, records, and engagements.
    /// </summary>
    /// <param name="objectType">The type of object to search, such as "tickets" </param>
    /// <param name="request">Search, filter, and sort parameters.</param>
    /// <returns></returns>
    [Post("/crm/v3/objects/{objectType}/search")]
    Task<SearchResults> SearchAsync(string objectType, SearchRequest request);

    /// <summary>
    /// Returns associations for a given object.
    /// </summary>
    /// <remarks>
    /// See <see href="https://developers.hubspot.com/docs/api/crm/associations"/>
    /// </remarks>
    /// <param name="fromObjectType">The type of object to get associations for.</param>
    /// <param name="fromObjectId">The Id of the from object.</param>
    /// <param name="toObjectType">The type of object to get associations to.</param>
    [Get("/crm/v4/objects/{fromObjectType}/{fromObjectId}/associations/{toObjectType}")]
    Task<HubSpotApiResults<HubSpotAssociation>> GetAssociationsAsync(
        HubSpotObjectType fromObjectType,
        long fromObjectId,
        HubSpotObjectType toObjectType);

    /// <summary>
    /// Returns information about a single HubSpot message.
    /// </summary>
    /// <remarks>
    /// See <see href="https://developers.hubspot.com/docs/api/conversations/conversations">Get A Single Message</see>.
    /// </remarks>
    /// <param name="threadId">The Id of the thread.</param>
    /// <param name="messageId">The message Id.</param>
    /// <returns></returns>
    [Get("/conversations/v3/conversations/threads/{threadId}/messages/{messageId}")]
    Task<HubSpotMessage?> GetMessageAsync(long threadId, string messageId);

    /// <summary>
    /// Adds a comment to an existing HubSpot thread.
    /// </summary>
    /// <remarks>
    /// See <see href="https://developers.hubspot.com/docs/api/conversations/conversations#add-comments-to-threads"/>
    /// </remarks>
    /// <param name="threadId">The HubSpot Conversation thread id.</param>
    /// <param name="comment"></param>
    /// <returns></returns>
    [Post("/conversations/v3/conversations/threads/{threadId}/messages")]
    Task<HubSpotMessage> CreateCommentAsync(long threadId, HubSpotComment comment);
}

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum HubSpotObjectType
{
    [EnumMember(Value = "conversation")]
    Conversation,

    [EnumMember(Value = "ticket")]
    Ticket,
}

/// <summary>
/// Client for <see href="https://legacydocs.hubspot.com/docs/methods/forms/submit_form_v3_authentication">HubSpot
/// Forms API</see>.
/// </summary>
public interface IHubSpotFormsClient
{
    public static readonly Uri ApiUrl = new("https://api.hsforms.com");

    /// <summary>
    /// Send form submission data to HubSpot using authentication.
    /// </summary>
    /// <remarks>
    /// Afterwards, we poll HubSpot for the associated ticket. See <see cref="HubSpotLinkerJob"/>.
    /// </remarks>
    /// <param name="portalId">The Id of the Portal the form belongs to.</param>
    /// <param name="formGuid">The Id of the HubSpot form.</param>
    /// <param name="request">The request body containing the form fields to submit.</param>
    [Post("/submissions/v3/integration/secure/submit/{portalId}/{formGuid}")]
    Task<SubmitResponse> SubmitAsync(long portalId, string formGuid, HubSpotFormSubmissionRequest request);
}

/// <summary>
/// Information about the HubSpot account associated with the current OAuth token.
/// </summary>
/// <param name="PortalId">The Portal Id.</param>
/// <param name="TimeZone">The TimeZone for the Portal.</param>
/// <param name="Currency">The currency.</param>
/// <param name="UtcOffsetMilliseconds">The UTC Offset in milliseconds</param>
/// <param name="UtcOffset">The UTC Offset</param>
public record HubSpotAccount(

    [property: JsonProperty("portalId")]
    [property: JsonPropertyName("portalId")]
    string PortalId,

    [property: JsonProperty("timeZone")]
    [property: JsonPropertyName("timeZone")]
    string TimeZone,

    [property: JsonProperty("currency")]
    [property: JsonPropertyName("currency")]
    string Currency,

    [property: JsonProperty("utcOffsetMilliseconds")]
    [property: JsonPropertyName("utcOffsetMilliseconds")]
    long UtcOffsetMilliseconds,

    [property: JsonProperty("utcOffset")]
    [property: JsonPropertyName("utcOffset")]
    string UtcOffset);

public record SubmitResponse(
    [property: JsonProperty("redirectUri")]
    [property: JsonPropertyName("redirectUri")]
    string? RedirectUri,

    [property: JsonProperty("inlineMessage")]
    [property: JsonPropertyName("inlineMessage")]
    string? InlineMessage,

    [property: JsonProperty("errors")]
    [property: JsonPropertyName("errors")]
    IReadOnlyList<HubSpotError> Errors);

/// <summary>
/// Information about the error when calling the Hubspot API and an ApiException is thrown. This is deserialized from
/// the <see cref="ApiException.Content"/> property.
/// </summary>
/// <param name="Status">The status (such as "error").</param>
/// <param name="Message">The message.</param>
/// <param name="CorrelationId">Dunno.</param>
/// <param name="Category">The type of error such as VALIDATION_ERROR.</param>
public record HubspotApiError(
    [property: JsonProperty("status")]
    [property: JsonPropertyName("status")]
    string Status,

    [property: JsonProperty("message")]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonProperty("correlationId")]
    [property: JsonPropertyName("correlationId")]
    string CorrelationId,

    [property: JsonProperty("category")]
    [property: JsonPropertyName("category")]
    string? Category)
{
    [property: JsonProperty("errors")]
    [property: JsonPropertyName("errors")]
    public IReadOnlyList<HubSpotError> Errors { get; init; } = Array.Empty<HubSpotError>();
}

public record HubSpotError(
    [property: JsonProperty("message")]
    [property: JsonPropertyName("message")]
    string Message,

    [property: JsonProperty("errorType")]
    [property: JsonPropertyName("errorType")]
    string ErrorType);

public record SearchRequest(
    [property: JsonProperty("filterGroups")]
    [property: JsonPropertyName("filterGroups")]
    IReadOnlyList<SearchFilterGroup> FilterGroups)
{
    public SearchRequest(SearchFilterGroup filterGroup)
        : this(new[] { filterGroup })
    {
    }

    public SearchRequest(SearchFilterGroup filterGroup, SearchFilterGroup filterGroup2)
        : this(new[] { filterGroup, filterGroup2 })
    {
    }

    public SearchRequest(SearchFilterGroup filterGroup, SearchFilterGroup filterGroup2, SearchFilterGroup filterGroup3)
        : this(new[] { filterGroup, filterGroup2, filterGroup3 })
    {
    }

    /// <summary>
    /// Constructs a <see cref="SearchRequest"/> with a single search filter.
    /// </summary>
    /// <param name="searchFilter">The search filter to use.</param>
    public SearchRequest(SearchFilter searchFilter)
        : this(new SearchFilterGroup(searchFilter))
    {
    }

    /// <summary>
    /// Constructs a <see cref="SearchRequest"/> with a single search criterion.
    /// </summary>
    /// <param name="propertyName">The property to search.</param>
    /// <param name="searchOperator">The search operator to use.</param>
    /// <param name="value">The value to search.</param>
    public SearchRequest(string propertyName, SearchOperator searchOperator, string value)
        : this(new SearchFilter(propertyName, searchOperator, value))
    {
    }

    [JsonProperty("sorts")]
    [JsonPropertyName("sorts")]
    public IEnumerable<SearchSort>? Sorts { get; init; }

    /// <summary>
    /// If specified, overrides the default set of properties returned in the search result with the ones
    /// specified here.
    /// </summary>
    [JsonProperty("properties")]
    [JsonPropertyName("properties")]
    public IReadOnlyList<string>? Properties { get; init; }

    /// <summary>
    /// Sets the number of results to return per page. The default is 10. The maximum is 100.
    /// </summary>
    [JsonProperty("limit")]
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    /// <summary>
    /// Retrieve the next page of results. If setting the limit to 20, to get page 2, set this to "20".
    /// </summary>
    [JsonProperty("after")]
    [JsonPropertyName("after")]
    public string? After { get; init; }
};

public record SearchFilterGroup(
    [property: JsonProperty("filters")]
    [property: JsonPropertyName("filters")]
    IReadOnlyList<SearchFilter> Filters)
{
    public SearchFilterGroup(SearchFilter filter) : this(new[] { filter })
    {
    }
}

public record SearchFilter(
    [property: JsonProperty("propertyName")]
    [property: JsonPropertyName("propertyName")]
    string PropertyName,

    [property: JsonProperty("operator")]
    [property: JsonPropertyName("operator")]
    SearchOperator Operator,

    [property: JsonProperty("value")]
    [property: JsonPropertyName("value")]
    string Value);

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum SearchOperator
{
    [EnumMember(Value = "LT")]
    LessThan,

    [EnumMember(Value = "LTE")]
    LessThanOrEqualTo,

    [EnumMember(Value = "GT")]
    GreaterThan,

    [EnumMember(Value = "GTE")]
    GreaterThanOrEqualTo,

    [EnumMember(Value = "EQ")]
    EqualTo,

    [EnumMember(Value = "NEQ")]
    NotEqualTo,

    [EnumMember(Value = "IN")]
    In,

    [EnumMember(Value = "NOT_IN")]
    NotIn,

    [EnumMember(Value = "Between")]
    Between,

    [EnumMember(Value = "HAS_PROPERTY")]
    HasProperty,

    [EnumMember(Value = "NOT_HAS_PROPERTY")]
    NotHasProperty,

    [EnumMember(Value = "CONTAINS_TOKEN")]
    ContainsToken,

    [EnumMember(Value = "NOT_CONTAINS_TOKEN")]
    NotContainsToken,
}

public record SearchSort(
    [property: JsonProperty("propertyName")]
    [property: JsonPropertyName("propertyName")]
    string PropertyName,

    [property: JsonProperty("order")]
    [property: JsonPropertyName("order")]
    SearchOrder Order);

[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum SearchOrder
{
    [EnumMember(Value = "ASCENDING")]
    Ascending,

    [EnumMember(Value = "DESCENDING")]
    Descending,
}

/// <summary>
/// Base type for a lot of HubSpot API .
/// </summary>
/// <param name="Results">The results of the API.</param>
/// <typeparam name="T">The individual item type of the results.</typeparam>
public record HubSpotApiResults<T>(

    [property: JsonProperty("results")]
    [property: JsonPropertyName("results")]
    IReadOnlyList<T> Results);

public record SearchResults(
    [property: JsonProperty("total")]
    [property: JsonPropertyName("total")]
    int Total,

    IReadOnlyList<HubSpotSearchResult> Results) : HubSpotApiResults<HubSpotSearchResult>(Results);

public record HubSpotSearchResult(
    string Id,

    [property: JsonProperty("properties")]
    [property: JsonPropertyName("properties")]
    IReadOnlyDictionary<string, string?> Properties,

    string CreatedAt,

    string UpdatedAt,

    bool Archived) : HubSpotObject(Id, CreatedAt, UpdatedAt, Archived);

public record HubSpotObject(
    [property: JsonProperty("id")]
    [property: JsonPropertyName("id")]
    string Id,

    [property: JsonProperty("createdAt")]
    [property: JsonPropertyName("createdAt")]
    string CreatedAt,

    [property: JsonProperty("updatedAt")]
    [property: JsonPropertyName("updatedAt")]
    string UpdatedAt,

    [property: JsonProperty("archived")]
    [property: JsonPropertyName("archived")]
    bool Archived);

/// <summary>
/// A HubSpot comment to add to a Conversation thread.
/// </summary>
/// <param name="Text">The Text of the comment.</param>
/// <param name="RichText">The optional HTML formatted comment.</param>
public record HubSpotComment(
    [property: JsonProperty("text")]
    [property: JsonPropertyName("text")]
    string Text,

    [property: JsonProperty("richText")]
    [property: JsonPropertyName("richText")]
    string? RichText = null)
{
    [JsonProperty("type")]
    [JsonPropertyName("type")]
    public string Type { get; } = "COMMENT";
};

public record HubSpotCommentResponse(
    string Id,
    string CreatedAt,
    string UpdatedAt,
    bool Archived,

    [property: JsonProperty("createdBy")]
    [property: JsonPropertyName("createdBy")]
    string CreatedBy,

    [property: JsonProperty("client")]
    [property: JsonPropertyName("client")]
    HubSpotClient Client,

    [property: JsonProperty("senders")]
    [property: JsonPropertyName("senders")]
    IReadOnlyList<HubSpotSender> Senders,

    [property: JsonProperty("recipients")]
    [property: JsonPropertyName("recipients")]
    IReadOnlyList<HubSpotRecipient> Recipients,

    [property: JsonProperty("text")]
    [property: JsonPropertyName("text")]
    string Text,

    [property: JsonProperty("richText")]
    [property: JsonPropertyName("richText")]
    string RichText,

    [property: JsonProperty("type")]
    [property: JsonPropertyName("type")]
    string Type) : HubSpotObject(Id, CreatedAt, UpdatedAt, Archived);

public record HubSpotMessage(
    string Id,
    string CreatedAt,
    string UpdatedAt,
    bool Archived,
    string CreatedBy,
    HubSpotClient Client,
    IReadOnlyList<HubSpotSender> Senders,
    IReadOnlyList<HubSpotRecipient> Recipients,
    string Text,
    string RichText,
    string Type,

    [property: JsonProperty("subject")]
    [property: JsonPropertyName("subject")]
    string Subject,

    [property: JsonProperty("truncationStatus")]
    [property: JsonPropertyName("truncationStatus")]
    string TruncationStatus,

    [property: JsonProperty("inReplyToId")]
    [property: JsonPropertyName("inReplyToId")]
    string InReplyToId,

    [property: JsonProperty("status")]
    [property: JsonPropertyName("status")]
    HubSpotMessageStatus Status,

    [property: JsonProperty("direction")]
    [property: JsonPropertyName("direction")]
    string Direction,

    [property: JsonProperty("channelId")]
    [property: JsonPropertyName("channelId")]
    string ChannelId,

    [property: JsonProperty("channelAccountId")]
    [property: JsonPropertyName("channelAccountId")]
    string ChannelAccountId) : HubSpotCommentResponse(
        Id,
        CreatedAt,
        UpdatedAt,
        Archived,
        CreatedBy,
        Client,
        Senders,
        Recipients,
        Text,
        RichText,
        Type);

public record HubSpotMessageStatus(
    [property: JsonProperty("statusType")]
    [property: JsonPropertyName("statusType")]
    string StatusType);

public record HubSpotClient(
    [property: JsonProperty("clientType")]
    [property: JsonPropertyName("clienttype")]
    string ClientType,

    [property: JsonProperty("integrationAppId")]
    [property: JsonPropertyName("integrationAppId")]
    string? IntegrationAppId = null);

public record HubSpotSender(
    [property: JsonProperty("actorId")]
    [property: JsonPropertyName("actorId")]
    string ActorId,

    [property: JsonProperty("name")]
    [property: JsonPropertyName("name")]
    string Name,

    [property: JsonProperty("senderField")]
    [property: JsonPropertyName("senderField")]
    string SenderField,

    [property: JsonProperty("deliveryIdentifier")]
    [property: JsonPropertyName("deliveryIdentifier")]
    DeliveryIdentifier DeliveryIdentifier
);

public record HubSpotRecipient(
    [property: JsonProperty("actorId")]
    [property: JsonPropertyName("actorId")]
    string ActorId,

    [property: JsonProperty("recipientField")]
    [property: JsonPropertyName("recipientField")]
    string RecipientField,

    [property: JsonProperty("deliveryIdentifiers")]
    [property: JsonPropertyName("deliveryIdentifiers")]
    IReadOnlyList<DeliveryIdentifier> DeliveryIdentifiers
);

public record DeliveryIdentifier(
    [property: JsonProperty("type")]
    [property: JsonPropertyName("type")]
    string Type,

    [property: JsonProperty("value")]
    [property: JsonPropertyName("value")]
    string Value
);

#pragma warning disable CA1008
public enum HubSpotAssociationTypeId
#pragma warning restore CA1008
{
    TicketToContact = 16,
    TicketToCompany = 26,
    ConversationToTicket = 31,
    TicketToConversation = 32,
}

public record CreateHubSpotAssociationType(
    [property: JsonProperty("associationTypeId")]
    [property: JsonPropertyName("associationTypeId")]
    int TypeId,

    [property: JsonProperty("associationCategory")]
    [property: JsonPropertyName("associationCategory")]
    string Category)
{
    public CreateHubSpotAssociationType(HubSpotAssociationTypeId typeId, string category = "HUBSPOT_DEFINED")
        : this((int)typeId, category)
    {
    }
}

public record CreateHubSpotAssociation(
    [property: JsonProperty("to")]
    [property: JsonPropertyName("to")]
    CrmObjectId To,

    [property: JsonProperty("types")]
    [property: JsonPropertyName("types")]
    IReadOnlyList<CreateHubSpotAssociationType> Types);

public record HubSpotAssociationType(
    [property: JsonProperty("category")]
    [property: JsonPropertyName("category")]
    string Category,

    [property: JsonProperty("typeId")]
    [property: JsonPropertyName("typeId")]
    int TypeId,

    [property: JsonProperty("label")]
    [property: JsonPropertyName("label")]
    string? Label);

public record HubSpotAssociation(
    [property: JsonProperty("toObjectId")]
    [property: JsonPropertyName("toObjectId")]
    long ToObjectId,

    [property: JsonProperty("associationTypes")]
    [property: JsonPropertyName("associationTypes")]
    IReadOnlyList<HubSpotAssociationType> AssociationTypes);

static partial class HubSpotClientLoggerExtensions
{
    [LoggerMessage(
        EventId = 4141,
        Level = LogLevel.Warning,
        Message = "HubSpot request failed.")]
    public static partial void RequestFailed(this ILogger logger, Exception ex);
}
