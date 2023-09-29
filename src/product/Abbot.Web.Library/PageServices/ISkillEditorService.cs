using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.PageServices;

/// <summary>
/// Encapsulates the functionality needed by the skill editor UI (Pages/Skills/Edit.cshtml.cs)
/// to create and save a skill.
/// </summary>
public interface ISkillEditorService
{
    /// <summary>
    /// Retrieves the skill by name and organization.
    /// </summary>
    /// <param name="name">The name of the entity.</param>
    /// <param name="organization">The organization the entity belongs to.</param>
    Task<Skill?> GetAsync(string name, Organization organization);

    /// <summary>
    /// Uses the <see cref="SkillUpdateModel"/> to create a new <see cref="Skill"/>.
    /// </summary>
    /// <param name="language">The language of the skill to create.</param>
    /// <param name="updateModel">The changes to apply to the skill.</param>
    /// <param name="creator">The user modifying the skill.</param>
    /// <param name="organization">The organization the skill belongs to.</param>
    Task<SkillCreateResult> CreateAsync(
        CodeLanguage language,
        SkillUpdateModel updateModel,
        User creator,
        Organization organization);

    /// <summary>
    /// Applies the changes in the <see cref="SkillUpdateModel"/> to the <see cref="Skill"/> and
    /// saves the changes. It creates a version snapshot (<see cref="SkillVersion"/> of the current
    /// version before applying changes.
    /// </summary>
    /// <param name="original">The original skill before the changes have been applied.</param>
    /// <param name="updateModel">The changes to apply to the skill.</param>
    /// <param name="modifiedBy">The user modifying the skill.</param>
    /// <returns></returns>
    Task<SkillUpdateResult> UpdateAsync(Skill original, SkillUpdateModel updateModel, User modifiedBy);
}

/// <summary>
/// Represents the result of creating a skill.
/// </summary>
/// <param name="CompiledSkill">The compiled skill, if compilation succeeded. If <paramref name="CompilationErrors"/> is non-empty, this will be null.</param>
/// <param name="CompilationErrors">A list of any compilation errors that occurred when saving the skill. If non-empty, <paramref name="CompiledSkill"/> will be null.</param>
public record SkillCreateResult(Skill? CompiledSkill, IReadOnlyList<ICompilationError> CompilationErrors);

/// <summary>
/// Represents the result of updating a skill.
/// </summary>
/// <param name="Saved"><c>true</c> if there were changes to save and they were saved. Otherwise <c>false</c>.</param>
/// <param name="CompilationErrors">A list of any compilation errors that occurred when saving the skill. If non-empty, the skill was not saved.</param>
public record SkillUpdateResult(bool Saved, IReadOnlyList<ICompilationError> CompilationErrors);
