using System;
using System.Diagnostics.CodeAnalysis;
using Serious.Abbot.Scripting;
using Serious.Abbot.Storage;

namespace Serious.Abbot.Functions.Messages;

/// <summary>
/// Represents a piece of data stored for a skill.
/// </summary>
public class SkillDataItem : ISkillDataItem
{
    readonly string _jsonValue;
    readonly IBrainSerializer _brainSerializer;
    readonly Lazy<dynamic> _dynamicValue;

    public SkillDataItem(string key, string jsonValue, IBrainSerializer brainSerializer)
    {
        _jsonValue = jsonValue;
        _brainSerializer = brainSerializer;
        Key = key;
        _dynamicValue = new Lazy<object>(
            () => brainSerializer.Deserialize(jsonValue) ?? jsonValue);
    }

    /// <summary>
    /// The key
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// A dynamic value. Could be a string or an object.
    /// </summary>
    public dynamic Value => _dynamicValue.Value;

    /// <summary>
    /// Retrieves the value as type T.
    /// </summary>
    /// <typeparam name="T">The type to cast the value as.</typeparam>
    [return: MaybeNull]
    public T GetValueAs<T>(T? defaultValue = default)
    {
        return _brainSerializer.Deserialize<T>(_jsonValue) ?? defaultValue;
    }
}
