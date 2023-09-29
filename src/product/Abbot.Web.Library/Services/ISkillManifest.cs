using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Metadata;

namespace Serious.Abbot.Services;

/// <summary>
/// Represents a complete manifest of all skills in the system including
/// aliases, user defined skills, and built-in skills.
/// </summary>
public interface ISkillManifest
{
    /// <summary>
    /// Get a user skill by id. This is used when we have the ID such as when the user is interacting
    /// with interactive elements such as a button.
    /// </summary>
    /// <param name="id">The Id of the skill.</param>
    Task<Skill?> GetSkillByIdAsync(Id<Skill> id);

    /// <summary>
    /// Given a name and organization, resolves the skill that should respond to that skill name.
    /// </summary>
    /// <param name="name">The skill name to resolve</param>
    /// <param name="organization">The current organization.</param>
    /// <param name="actor">The <see cref="IFeatureActor"/> to use for feature-flag checks.</param>
    Task<IResolvedSkill?> ResolveSkillAsync(string name, Organization organization, IFeatureActor actor);

    /// <summary>
    /// Given a <see cref="Skill"/> and arguments, resolves the skill that should respond to the interaction.
    /// </summary>
    /// <param name="skill">The user skill parsed from the Id stored in interaction Callback info.</param>
    /// <param name="appendedArguments">The arguments to pass to the skill.</param>
    /// <param name="actor">The feature actor.</param>
    Task<ResolvedSkill> ResolveSkillAsync(Skill skill, string appendedArguments, IFeatureActor actor);

    /// <summary>
    /// Retrieves all skills for the current organization.
    /// </summary>
    /// <param name="organization">The current organization.</param>
    /// <param name="actor">The <see cref="IFeatureActor"/> to use for feature-flag checks.</param>
    Task<IReadOnlyList<ISkillDescriptor>> GetAllSkillDescriptorsAsync(Organization organization, IFeatureActor actor);
}
