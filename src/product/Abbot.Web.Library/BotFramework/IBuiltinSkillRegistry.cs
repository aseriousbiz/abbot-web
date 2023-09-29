using System.Collections.Generic;
using Serious.Abbot.Metadata;

namespace Serious.Abbot.Services;

/// <summary>
/// A registry of all the built-in skills.
/// </summary>
public interface IBuiltinSkillRegistry
{
    /// <summary>
    /// Retrieves a descriptor for the built-in skill by name.
    /// </summary>
    /// <param name="name">The name of the skill (lowercase)</param>
    IBuiltinSkillDescriptor? this[string name] { get; }

    /// <summary>
    /// Returns a list of all the built-in skill descriptors.
    /// </summary>
    IReadOnlyList<IBuiltinSkillDescriptor> SkillDescriptors { get; }
}
