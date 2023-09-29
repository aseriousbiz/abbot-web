using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;
using JsonConverter = Newtonsoft.Json.JsonConverter;

namespace Serious.Slack.Payloads;

/// <summary>
/// Represents a "macro mention" like <c>@channel</c>, <c>@everyone</c>, and <c>@here</c>
/// </summary>
/// <param name="Range">The range of the broadcast. Values are <c>@channel</c>, <c>@everyone</c>, and <c>@here</c></param>
[Element("broadcast")]
public sealed record Broadcast(
    [property: JsonProperty("range")][property: JsonPropertyName("range")] BroadcastRange Range) : Element("broadcast")
{
    /// <summary>
    /// Constructs a <see cref="Broadcast"/> with the range set to <see cref="BroadcastRange.Here"/>
    /// </summary>
    public Broadcast() : this(BroadcastRange.Here)
    {
    }
}

/// <summary>
/// Represents a broadcast range, with an optional label.
/// </summary>
/// <param name="Type">A <see cref="BroadcastRangeType"/> indicating the type of broadcast range.</param>
/// <param name="Label">An optional label to be displayed for the range.</param>
[Newtonsoft.Json.JsonConverter(typeof(BroadcastRangeConverter))]
public record struct BroadcastRange(BroadcastRangeType Type, string? Label)
{
    /// <summary>
    /// Represents a broadcast range of <c>@channel</c>
    /// </summary>
    public static BroadcastRange Channel => new(BroadcastRangeType.Channel, "channel");

    /// <summary>
    /// Represents a broadcast range of <c>@everyone</c>
    /// </summary>
    public static BroadcastRange Everyone => new(BroadcastRangeType.Everyone, "everyone");

    /// <summary>
    /// Represents a broadcast range of <c>@here</c>
    /// </summary>
    public static BroadcastRange Here => new(BroadcastRangeType.Here, "here");
}

/// <summary>
/// The macro mention ranges that can be used in a <see cref="Broadcast"/>,
/// such as <c>@channel</c>, <c>@everyone</c>, and <c>@here</c>.
/// </summary>
public enum BroadcastRangeType
{
    /// <summary>
    /// Everyone in the channel (<c>@channel</c>).
    /// </summary>
    Channel,

    /// <summary>
    /// Everyone. (<c>@everyone</c>).
    /// </summary>
    Everyone,

    /// <summary>
    /// Everyone currently in the channel (<c>@here</c>).
    /// </summary>
    Here
}

class BroadcastRangeConverter : JsonConverter
{
    static readonly Dictionary<string, BroadcastRangeType> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        {"@channel", BroadcastRangeType.Channel},
        {"channel", BroadcastRangeType.Channel},
        {"@everyone", BroadcastRangeType.Everyone},
        {"everyone", BroadcastRangeType.Everyone},
        {"@here", BroadcastRangeType.Here},
        {"here", BroadcastRangeType.Here}
    };

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanConvert(Type objectType) => objectType == typeof(BroadcastRange);

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType is not JsonToken.String || reader.Value is null)
        {
            throw new JsonSerializationException("Expected a string");
        }

        var str = (string)reader.Value;
        var splat = str.Split('|');
        var type = TypeMap.TryGetValue(splat[0], out var t)
            ? t
            : throw new JsonSerializationException($"Unknown broadcast range type: {splat[0]}");
        var label = splat.Length > 1 ? splat[1] : null;
        return new BroadcastRange(type, label);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is not BroadcastRange range)
        {
            throw new JsonSerializationException($"Expected {nameof(BroadcastRange)}");
        }

        var typeStr = range.Type.ToString().ToLowerInvariant();
        var valStr = range.Label is not null ? $"{typeStr}|{range.Label}" : typeStr;
        writer.WriteValue(valStr);
    }
}
