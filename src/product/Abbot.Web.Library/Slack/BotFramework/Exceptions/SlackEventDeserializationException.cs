using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Abbot.Exceptions;

/// <summary>
/// Custom exception thrown when we can't deserialize an incoming Slack payload.
/// </summary>
public class SlackEventDeserializationException : Exception
{
    public SlackEventDeserializationException() : base("Could not deserialize Slack event")
    {
        ProtectedJson = "";
    }

    public SlackEventDeserializationException(string message) : base(message)
    {
        ProtectedJson = "";
    }

    public SlackEventDeserializationException(string message, Exception innerException) : base(message, innerException)
    {
        ProtectedJson = "";
    }

    /// <summary>
    /// Creates an exception with as much information that we can extract from the JSON payload.
    /// </summary>
    /// <returns></returns>
    public static SlackEventDeserializationException Create(
        string json,
        Type targetType,
        string protectedJson,
        Exception innerException)
    {
        var eventInfo = GetEventInfo(json);

        return new SlackEventDeserializationException(protectedJson, targetType, eventInfo, innerException);
    }

    SlackEventDeserializationException(
        string protectedJson,
        Type targetType,
        SimpleEventEnvelope? eventEnvelope,
        Exception innerException) : base(CreateMessage(eventEnvelope, targetType, protectedJson), innerException)
    {
        ProtectedJson = protectedJson;
        EventEnvelope = eventEnvelope;
    }

    static SimpleEventEnvelope? GetEventInfo(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<SimpleEventEnvelope>(json);
        }
        catch (JsonException)
        {
            // Ignore and go back to our original approach.
        }

        return null;
    }

    static string CreateMessage(SimpleEventEnvelope? eventEnvelope, Type targetType, string protectedValue)
    {
        return eventEnvelope is null
            ? $"Could not deserialize JSON to {targetType.Name}.\n\n{protectedValue}"
            : $"Could not deserialize event JSON (EventEnvelope: {eventEnvelope}) to {targetType.Name}.\n\n{protectedValue}";
    }

    public string ProtectedJson { get; }

    public SimpleEventEnvelope? EventEnvelope { get; }

    public record SimpleEventEnvelope(
        [property: JsonProperty("type")]
        [property: JsonPropertyName("type")]
        string? Type,

        [property: JsonProperty("team_id")]
        [property: JsonPropertyName("team_id")]
        string? TeamId,

        [property: JsonProperty("event_id")]
        [property: JsonPropertyName("event_id")]
        string? EventId,

        [property: JsonProperty("event")]
        [property: JsonPropertyName("event")]
        SimpleEventBody? Event);

    public record SimpleEventBody(
        [property: JsonProperty("type")]
        [property: JsonPropertyName("type")]
        string? Type,

        [property: JsonProperty("ts")]
        [property: JsonPropertyName("ts")]
        string? Timestamp,

        [property: JsonProperty("channel")]
        [property: JsonPropertyName("channel")]
        string? Channel,

        [property: JsonProperty("user")]
        [property: JsonPropertyName("user")]
        string? User);
}
