using System.Diagnostics.CodeAnalysis;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents a piece of data stored for a skill.
/// </summary>
public interface ISkillDataItem
{
    /// <summary>
    /// The key
    /// </summary>
    string Key { get; }

    /// <summary>
    /// A dynamic value. Could be a string or an object.
    /// </summary>
    dynamic Value { get; }

    /// <summary>
    /// Retrieves the value as type T.
    /// </summary>
    /// <param name="defaultValue">The value to return if value does not exist or cannot be cast.</param>
    /// <typeparam name="T">The type to cast the value as.</typeparam>
#pragma warning disable CS8601
    [return: MaybeNull]
    T GetValueAs<T>(T? defaultValue = default);
}
