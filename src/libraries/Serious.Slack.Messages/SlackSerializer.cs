using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Refit;
using Serious.Slack.BlockKit;

namespace Serious;

/// <summary>
/// Static class containing the serialization settings for the Slack types.
/// </summary>
public static class SlackSerializer
{
    /// <summary>
    /// The serialization settings we use when serializing Slack types.
    /// </summary>
    public static readonly JsonSerializerSettings SerializationSettings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        ContractResolver = EmptyCollectionResolver.Instance,
    };

    /// <summary>
    /// The Refit settings we use for all our clients.
    /// </summary>
    public static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new NewtonsoftJsonContentSerializer(SerializationSettings)
    };

    /// <summary>
    /// Method to serialize a Slack object to JSON.
    /// </summary>
    /// <param name="value">A Slack object or collection containing slack objects.</param>
    /// <param name="formatting">Indicates how the output should be formatted.</param>
    /// <returns>A JSON representation of the slack object.</returns>
    public static string Serialize(object value, Formatting formatting = default)
    {
        var jsonSerializer = JsonSerializer.CreateDefault(SerializationSettings);
        jsonSerializer.Formatting = formatting;

        // Align indentation with Slack's default (when formatting is Indented)
        // https://github.com/JamesNK/Newtonsoft.Json/blob/d0a328e8a46304d62d2174b8bba54721d02be3d3/Src/Newtonsoft.Json/JsonConvert.cs#L657-L669
        var sb = new StringBuilder(256);
        var sw = new StringWriter(sb, CultureInfo.InvariantCulture);
        using (JsonTextWriter jsonWriter = new JsonTextWriter(sw) { Indentation = 4 })
        {
            jsonWriter.Formatting = jsonSerializer.Formatting;

            jsonSerializer.Serialize(jsonWriter, value, null);
        }

        return sw.ToString();
    }

    /// <summary>
    /// Method to deserialize a Slack object from JSON.
    /// </summary>
    /// <param name="json">A JSON representation of the Slack object or collection.</param>
    /// <returns>A Slack object or collection containing slack objects.</returns>
    public static T? Deserialize<T>(string json)
    {
        return JsonConvert.DeserializeObject<T>(json, SerializationSettings);
    }
}

/// <summary>
/// A contract resolver that omits empty generic collections when serializing to JSON.
/// </summary>
public class EmptyCollectionResolver : DefaultContractResolver
{
    /// <summary>
    /// The singleton empty instance.
    /// </summary>
    public static readonly EmptyCollectionResolver Instance = new();

    /// <summary>
    /// If the property is an empty collection, this makes sure it doesn't get serialized.
    /// </summary>
    protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
    {
        JsonProperty property = base.CreateProperty(member, memberSerialization);

        if (property.DeclaringType is { } declaringType
            && member is PropertyInfo propertyInfo
            && declaringType.IsAssignableTo(typeof(IMultiValueElement))
            && property.PropertyType is { } propertyType
            && propertyType.IsAssignableTo(typeof(IReadOnlyCollection<object>)))
        {
            property.ShouldSerialize = instance => {
                var value = propertyInfo.GetValue(instance);
                return value switch
                {
                    IReadOnlyCollection<object> collection => collection.Count > 0,
                    ICollection collection => collection.Count > 0,
                    null => false,
                    _ => true
                };
            };
        }

        return property;
    }
}
