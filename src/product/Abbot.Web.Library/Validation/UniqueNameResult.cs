using Serious.Abbot.Entities;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Validation;

/// <summary>
/// Provides information on whether a skill name is unique, or in conflict. In the conflict case,
/// returns information about the conflict type.
/// </summary>
/// <remarks>
/// The uniqueness of a skill name is bit more complex than you might expect because a skill name
/// can conflict with other skill types such as built-in, aliases, lists, etc.
/// </remarks>
public class UniqueNameResult
{
    public const string ReservedKeywordConflict = "Reserved";

    /// <summary>
    /// A <see cref="UniqueNameResult"/> that indicates the skill name is unique.
    /// </summary>
    public static UniqueNameResult Unique { get; } = new();

    /// <summary>
    /// A <see cref="UniqueNameResult"/> that indicates the skill name conflicts with another.
    /// </summary>
    public static UniqueNameResult Conflict(string conflictType) => new UniqueNameResult(conflictType);

    UniqueNameResult()
    {
        IsUnique = true;
    }

    UniqueNameResult(string conflictType)
    {
        ConflictType = conflictType;
    }

    /// <summary>
    /// Whether or not the skill name is unique.
    /// </summary>
    public bool IsUnique { get; }

    /// <summary>
    /// The type of skill that's in conflict, or null if there is no conflict.
    /// </summary>
    public string? ConflictType { get; }

    /// <summary>
    /// Retrieves a friendly name for the conflict type.
    /// </summary>
    public string ConflictTypeFriendlyName =>
        ConflictType switch
        {
            nameof(ISkill) => "Built-in skill",
            nameof(UserList) => "list",
            nameof(Skill) => "skill",
            nameof(Alias) => "alias",
            ReservedKeywordConflict => "reserved keyword",
            _ => "item"
        };
}
