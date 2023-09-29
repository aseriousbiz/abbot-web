using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Integrations.HubSpot;

/// <summary>
/// Payload for a HubSpot webhook.
/// </summary>
/// <param name="ObjectId">
/// The ID of the object that was created, changed, or deleted. For contacts this is the contact ID; for companies,
/// the company ID; for deals, the deal ID; and for conversations the thread ID
/// </param>
/// <param name="ChangeSource">
/// The source of the change. This can be any of the change sources that appear in contact property histories.
/// </param>
/// <param name="EventId">The ID of the event that triggered this notification. This value is not guaranteed to be unique.</param>
/// <param name="SubscriptionId">The ID of the subscription that triggered a notification about the event.</param>
/// <param name="PortalId">The customer's HubSpot account ID where the event occurred.</param>
/// <param name="AppId">
/// The ID of your application. This is used in case you have multiple applications pointing to the same webhook URL.
/// </param>
/// <param name="OccurredAt">When this event occurred as a millisecond timestamp.</param>
/// <param name="SubscriptionType">
/// The type of event this notification is for. Review the list of supported subscription types in the webhooks
/// subscription section above.
/// </param>
/// <param name="AttemptNumber">
/// Starting at 0, which number attempt this is to notify your service of this event. If your service times-out or
/// throws an error as describe in the Retries section below, HubSpot will attempt to send the notification up to 10
/// times with retries spread out over the next 24 hours.
/// </param>
/// <remarks>
/// https://developers.hubspot.com/docs/api/webhooks#webhooks-payloads
/// </remarks>
public record HubSpotWebhookPayload(

    [property: JsonProperty("objectId")]
    [property: JsonPropertyName("objectId")]
    long ObjectId,

    [property: JsonProperty("changeSource")]
    [property: JsonPropertyName("changeSource")]
    string ChangeSource,

    [property: JsonProperty("eventId")]
    [property: JsonPropertyName("eventId")]
    long EventId,

    [property: JsonProperty("subscriptionId")]
    [property: JsonPropertyName("subscriptionId")]
    long SubscriptionId,

    [property: JsonProperty("portalId")]
    [property: JsonPropertyName("portalId")]
    long PortalId,

    [property: JsonProperty("appId")]
    [property: JsonPropertyName("appId")]
    long AppId,

    [property: JsonProperty("occurredAt")]
    [property: JsonPropertyName("occurredAt")]
    long OccurredAt,

    [property: JsonProperty("subscriptionType")]
    [property: JsonPropertyName("subscriptionType")]
    string SubscriptionType,

    [property: JsonProperty("attemptNumber")]
    [property: JsonPropertyName("attemptNumber")]
    int AttemptNumber)
{
    /// <summary>
    /// This is only sent for property change subscriptions and is the name of the property that was changed.
    /// </summary>
    [JsonProperty("propertyName")]
    [JsonPropertyName("propertyName")]
    public string? PropertyName { get; init; }

    /// <summary>
    /// This is only sent for property change subscriptions and represents the new value set for the property that
    /// triggered the notification.
    /// </summary>
    [JsonProperty("propertyValue")]
    [JsonPropertyName("propertyValue")]
    public string? PropertyValue { get; init; }

    /// <summary>
    /// This is only sent when a webhook is listening for new messages to a thread. It is the ID of the new message.
    /// </summary>
    [JsonProperty("messageId")]
    [JsonPropertyName("messageId")]
    public string? MessageId { get; init; }

    /// <summary>
    /// This is only sent when a webhook is listening for new messages to a thread. It represents the type of message
    /// you're sending. This value can either be <c>MESSAGE</c> or <c>COMMENT</c>.
    /// </summary>
    public string? MessageType { get; init; }

    public string? ChangeFlag { get; set; }
}
