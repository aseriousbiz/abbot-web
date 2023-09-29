using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Metadata;

namespace Serious.Abbot.Services;

/// <summary>
/// A registry of all the built-in skills.
/// </summary>
public class BuiltinSkillRegistry : IBuiltinSkillRegistry
{
    readonly IReadOnlyDictionary<string, IBuiltinSkillDescriptor> _skillDescriptors;

    /// <summary>
    /// Constructs a <see cref="BuiltinSkillRegistry"/>.
    /// </summary>
    /// <param name="skillDescriptors"></param>
    public BuiltinSkillRegistry(IEnumerable<IBuiltinSkillDescriptor> skillDescriptors)
    {
        _skillDescriptors = skillDescriptors
            .ToDictionary(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase);
        SkillDescriptors = _skillDescriptors.Values.ToReadOnlyList();
    }

    /// <summary>
    /// Retrieves a descriptor for the built-in skill by name.
    /// </summary>
    /// <param name="name">The name of the skill (lowercase)</param>
    public IBuiltinSkillDescriptor? this[string name] =>
        _skillDescriptors.TryGetValue(name, out var value)
            ? value
            : null;

    /// <summary>
    /// Returns a list of all the built-in skill descriptors.
    /// </summary>
    public IReadOnlyList<IBuiltinSkillDescriptor> SkillDescriptors { get; }
}
