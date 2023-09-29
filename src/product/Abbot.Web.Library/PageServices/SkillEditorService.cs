using System.Linq;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.PageServices;

/// <summary>
/// Encapsulates the functionality needed by the skill editor UI (Pages/Skills/Edit.cshtml.cs)
/// to create and save a skill.
/// </summary>
public class SkillEditorService : ISkillEditorService
{
    readonly ISkillRepository _repository;
    readonly ISkillCompiler _skillCompiler;
    readonly IAssemblyCache _assemblyCacheWriter;

    public SkillEditorService(
        ISkillRepository repository,
        ISkillCompiler skillCompiler,
        IAssemblyCache assemblyCacheWriter)
    {
        _repository = repository;
        _skillCompiler = skillCompiler;
        _assemblyCacheWriter = assemblyCacheWriter;
    }

    /// <summary>
    /// Retrieves the skill by name and organization.
    /// </summary>
    /// <param name="name">The name of the entity.</param>
    /// <param name="organization">The organization the entity belongs to.</param>
    public Task<Skill?> GetAsync(string name, Organization organization)
    {
        return _repository.GetAsync(name, organization);
    }

    /// <summary>
    /// Uses the <see cref="SkillUpdateModel"/> to create a new <see cref="Skill"/>.
    /// </summary>
    /// <param name="language">The language of the skill to create.</param>
    /// <param name="updateModel">The changes to apply to the skill.</param>
    /// <param name="creator">The user modifying the skill.</param>
    /// <param name="organization">The organization the skill belongs to.</param>
    public async Task<SkillCreateResult> CreateAsync(
        CodeLanguage language,
        SkillUpdateModel updateModel,
        User creator,
        Organization organization)
    {
        var skill = new Skill
        {
            Language = language,
            Enabled = true,
            Organization = organization
        };
        updateModel.ApplyChanges(skill);
        if (language == CodeLanguage.CSharp)
        {
            var compilation = await CompileToAssemblyCache(skill.Code, organization);
            if (compilation.CompilationErrors.Any())
            {
                return new(null, compilation.CompilationErrors);
            }
        }
        await _repository.CreateAsync(skill, creator);
        return new(skill, Array.Empty<ICompilationError>());
    }

    /// <summary>
    /// Applies the changes in the <see cref="SkillUpdateModel"/> to the <see cref="Skill"/> and
    /// saves the changes. It creates a version snapshot (<see cref="SkillVersion"/> of the current
    /// version before applying changes.
    /// </summary>
    /// <param name="original">The original skill before the changes have been applied.</param>
    /// <param name="updateModel">The changes to apply to the skill.</param>
    /// <param name="modifiedBy">The user modifying the skill.</param>
    public async Task<SkillUpdateResult> UpdateAsync(Skill original, SkillUpdateModel updateModel, User modifiedBy)
    {
        if (original.Language == CodeLanguage.CSharp && updateModel.Code is not null)
        {
            // Only compile the code if it's C# AND the code changed.
            var compilation = await CompileToAssemblyCache(
                updateModel.Code,
                original.Organization);

            if (compilation.CompilationErrors.Any())
            {
                return new(false, compilation.CompilationErrors);
            }
            original.CacheKey = compilation.CompiledSkill.Name;
        }
        var success = await _repository.UpdateAsync(original, updateModel, modifiedBy);
        return new(success, Array.Empty<ICompilationError>());
    }

    async Task<ICompilationResult> CompileToAssemblyCache(string code, IOrganizationIdentifier organizationIdentifier)
    {
        var compilation = await _skillCompiler.CompileAsync(CodeLanguage.CSharp, code);
        if (!compilation.CompilationErrors.Any())
        {
            await _assemblyCacheWriter.WriteToCacheAsync(organizationIdentifier, compilation.CompiledSkill);
        }

        return compilation;
    }
}
