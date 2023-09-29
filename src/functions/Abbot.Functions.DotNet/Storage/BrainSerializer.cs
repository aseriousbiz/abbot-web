using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;

namespace Serious.Abbot.Storage;

/// <summary>
/// A serializer for the bot brain to store and retrieve data items for a skill.
/// </summary>
public class BrainSerializer : IBrainSerializer
{
    static readonly Regex RootTypeRegex = new(@"""\$type"":""(?<info>[^""]+)", RegexOptions.Compiled);
    public const string RoslynScriptCompiledAssemblyPrefix = "ℛ*";

    readonly ISkillContextAccessor _skillContextAccessor;

    JsonSerializerSettings? _brainSerializerReadSettings;
    JsonSerializerSettings? _brainSerializerWriteSettings;
    BrainSerializationBinder? _brainSerializationBinder;

    BrainSerializationBinder Binder => _brainSerializationBinder ??= new BrainSerializationBinder(SkillContext.AssemblyName);

    /// <summary>
    /// Constructs a <see cref="BrainSerializer"/> with serializer settings that use the Abbot
    /// <see cref="BrainSerializationBinder"/>.
    /// </summary>
    /// <param name="skillContextAccessor">Provides access to context needed for running the skill including the current assembly name.</param>
    public BrainSerializer(ISkillContextAccessor skillContextAccessor)
    {
        _skillContextAccessor = skillContextAccessor;
    }

    SkillContext SkillContext => _skillContextAccessor.SkillContext
        ?? throw new InvalidOperationException($"{nameof(SkillContextAccessor)}.{nameof(SkillContextAccessor.SkillContext)} must be set before accessing it.");

    JsonSerializerSettings BrainSerializerReadSettings => _brainSerializerReadSettings ??=
        new JsonSerializerSettings
        {
            // we tell newtonsoft to not do strict typing when reading from the json
            // in the cases where newtonsoft doesn't know what to do, we handle that with the converter
            TypeNameHandling = TypeNameHandling.None
        }
        .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        .ConfigureAnonymous(this)
        .ConfigureRecognizers();

    JsonSerializerSettings BrainSerializerWriteSettings => _brainSerializerWriteSettings ??=
        new JsonSerializerSettings
        {
#pragma warning disable CA2326
            // we write all type data into the json, always
            TypeNameHandling = TypeNameHandling.All
#pragma warning restore CA2326
        }
        .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);


    /// <summary>
    /// Deserializes the JSON to a .NET object using <see cref="_brainSerializerReadSettings"/>.
    /// </summary>
    /// <param name="value">The JSON to deserialize.</param>
    /// <returns>The deserialized object from the JSON string.</returns>
    public object? Deserialize(string value)
    {
        return IsComplexObject(value, out Type type)
            ? JsonConvert.DeserializeObject(value, type, BrainSerializerReadSettings) // use the type that's in the json so the returned object is not a JObject
            : JsonConvert.DeserializeObject(value, BrainSerializerReadSettings);
    }

    /// <summary>
    /// Deserializes the JSON to the specified .NET type.
    /// </summary>
    /// <param name="value">The JSON to deserialize.</param>
    public T? Deserialize<T>(string value)
    {
        return JsonConvert.DeserializeObject<T>(value, BrainSerializerReadSettings);
    }

    /// <summary>
    /// Deserializes the JSON to the specified .NET type.
    /// </summary>
    /// <param name="value">The JSON to deserialize.</param>
    /// <param name="type">The type to deserialize into.</param>
    /// <returns></returns>
    public object? Deserialize(string value, Type type)
    {
        return JsonConvert.DeserializeObject(value, type, BrainSerializerReadSettings);
    }

    /// <summary>
    /// Serializes the specified object to a JSON string retaining the .NET type name.
    /// </summary>
    /// <param name="value">The object to serialize.</param>
    /// <param name="withTypes"></param>
    /// <returns>A JSON string representation of the object.</returns>
    public string SerializeObject(object? value, bool withTypes = true)
    {
        return withTypes ? JsonConvert.SerializeObject(value, BrainSerializerWriteSettings) : JsonConvert.SerializeObject(value, BrainSerializerReadSettings);
    }

    bool IsComplexObject(string value, out Type type)
    {
        type = null!;

        // checks if the value is a complex object (i.e, is a json object with type information)
        // this is looking for something like {"$type":"System.Collections.Generic.List`1[[Submission#0+DbState, R*0ad31ee8-fec5-43bc-82d0-bc0a5b272917#21-0]], System.Private.CoreLib"
        var match = RootTypeRegex.Match(value);
        return match.Success && TryParseTypeAndAssembly(match.Groups["info"].Value, out type);
    }

    bool TryParseTypeAndAssembly(string? typeInfo, out Type type)
    {
        type = null!;

        if (typeInfo is null)
            return false;

        var (assemblyName, typeName) = BrainSerializationBinder.SplitFullyQualifiedTypeName(typeInfo);
        type = assemblyName?.StartsWith(RoslynScriptCompiledAssemblyPrefix, StringComparison.Ordinal) ?? false
            ? Binder.GetTypeByName(typeName, SkillContext.AssemblyName)
            : Binder.GetTypeByName(typeName, assemblyName);

        return true;
    }

    /// <summary>
    /// Converter that creates typed objects using the stored data types in the json when Deserialize asks for an `object` type.
    /// If the stored type is the user assembly, it uses the current assembly instead, so we don't have mismatched data types.
    /// </summary>
    internal class AnonymousObjectConverter : JsonConverter
    {
        readonly BrainSerializer _brainSerializer;

        public AnonymousObjectConverter(BrainSerializer serializer)
        {
            _brainSerializer = serializer;
        }

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            dynamic? t = token.Children().FirstOrDefault(x => ((dynamic)x).Name == "$type");
            string? typeInfo = t?.Value?.ToString() ?? null;
            return _brainSerializer.TryParseTypeAndAssembly(typeInfo, out var type)
                ? serializer.Deserialize(token.CreateReader(), type)
                : token.ToObject(typeof(JObject), serializer);
        }

        /// <summary>
        /// Mandatory override that does nothing because this converter only reads
        /// </summary>
        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
            throw new NotImplementedException();

        /// <summary>
        /// We handle all things that are specifically `object`.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public override bool CanConvert(Type objectType) => objectType == typeof(object);

        public override bool CanWrite => false;
    }
}

static class ConverterExtensions
{
    public static JsonSerializerSettings ConfigureAnonymous(this JsonSerializerSettings settings, BrainSerializer serializer)
    {
        settings.Converters.Add(new BrainSerializer.AnonymousObjectConverter(serializer));
        return settings;
    }

    public static JsonSerializerSettings ConfigureRecognizers(this JsonSerializerSettings settings)
    {
        settings.Converters.Add(new TimexConverter());
        return settings;
    }
}

public class TimexConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        return reader.Value is null ? new TimexProperty() : new TimexProperty(reader.Value.ToString());
    }

    public override bool CanConvert(Type objectType) => objectType == typeof(TimexProperty);

    public override bool CanWrite => false;
}
