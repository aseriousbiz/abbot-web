using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Metadata;
using Serious.Abbot.Repositories;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Services;

/// <summary>
/// Represents a complete manifest of all skills in the system including
/// aliases, user defined skills, and built-in skills.
/// </summary>
public class SkillManifest : ISkillManifest
{
    readonly IBuiltinSkillRegistry _builtinSkillRegistry;
    readonly ISkillRepository _skillRepository;
    readonly IListRepository _listRepository;
    readonly IAliasRepository _aliasRepository;
    readonly FeatureService _featureService;

    public SkillManifest(
        IBuiltinSkillRegistry builtinSkillRegistry,
        ISkillRepository skillRepository,
        IListRepository listRepository,
        IAliasRepository aliasRepository,
        FeatureService featureService)
    {
        _builtinSkillRegistry = builtinSkillRegistry;
        _skillRepository = skillRepository;
        _listRepository = listRepository;
        _aliasRepository = aliasRepository;
        _featureService = featureService;
    }

    /// <summary>
    /// Get a user skill by id. This is used when we have the ID such as when the user is interacting
    /// with interactive elements such as a button.
    /// </summary>
    /// <param name="id">The Id of the skill.</param>
    public Task<Skill?> GetSkillByIdAsync(Id<Skill> id) => _skillRepository.GetByIdAsync(id);

    /// <summary>
    /// Given a name and organization, resolves the skill that should respond to that skill name.
    /// </summary>
    /// <param name="name">The skill name to resolve</param>
    /// <param name="organization">The current organization.</param>
    /// <param name="actor">The <see cref="IFeatureActor"/> to use for feature-flag checks.</param>
    public async Task<IResolvedSkill?> ResolveSkillAsync(string name, Organization organization, IFeatureActor actor)
    {
        var resolvedBuiltIn = await ResolveBuiltInSkillAsync(name, string.Empty, actor);

        if (resolvedBuiltIn is not null)
        {
            return resolvedBuiltIn;
        }

        async Task<ResolvedSkill?> ResolveAsync(string skillName, string appendedArguments)
        {
            var skill = await _skillRepository.GetAsync(skillName, organization);
            return skill is not null
                ? await ResolveUserSkillAsync(skill, appendedArguments, actor)
                : null;
        }

        var resolvedSkill = await ResolveAsync(name, string.Empty);
        if (resolvedSkill is not null)
        {
            return resolvedSkill;
        }

        var list = await _listRepository.GetAsync(name, organization);
        if (list is not null)
        {
            var targetSkill = _builtinSkillRegistry[ListSkill.Name];
            if (targetSkill is null)
            {
                throw new InvalidOperationException(
                    "The built in ListSkill does not exist. What kind of clown show are you running here?!");
            }

            return new ResolvedSkill(ListSkill.Name, name, list.Description, targetSkill.Skill);
        }

        var alias = await _aliasRepository.GetAsync(name, organization);
        if (alias is not null)
        {
            var targetSkill = await ResolveBuiltInSkillAsync(alias.TargetSkill, alias.TargetArguments, actor)
                              ?? await ResolveAsync(alias.TargetSkill, alias.TargetArguments);
            if (targetSkill is not null)
            {
                return targetSkill.WithNameAndDescription(
                    targetSkill.Name,
                    alias.Description);
            }
        }

        return null;
    }

    /// <summary>
    /// Retrieves all skills for the current organization.
    /// </summary>
    /// <param name="organization">The current organization.</param>
    /// <param name="actor">The <see cref="IFeatureActor"/> to use for feature-flag checks.</param>
    public async Task<IReadOnlyList<ISkillDescriptor>> GetAllSkillDescriptorsAsync(Organization organization, IFeatureActor actor)
    {
        var builtIns = new List<ISkillDescriptor>();
        foreach (var descriptor in _builtinSkillRegistry.SkillDescriptors)
        {
            if (descriptor.Hidden)
            {
                continue;
            }

            if (descriptor.FeatureFlag is { Length: > 0 } &&
                !await _featureService.IsEnabledAsync(descriptor.FeatureFlag, actor))
            {
                continue;
            }

            if (descriptor.PlanFeature is not null && !organization.HasPlanFeature(descriptor.PlanFeature.Value))
            {
                continue;
            }

            builtIns.Add(descriptor);
        }

        var skills = await _skillRepository.GetQueryable(organization).Where(s => s.Enabled).ToListAsync();
        var listSkills = await _listRepository.GetAllAsync(organization);
        var aliases = await _aliasRepository.GetAllAsync(organization);

        return builtIns
            .Union(skills)
            .Union(listSkills)
            .Union(aliases)
            .ToReadOnlyList();
    }

    public Task<ResolvedSkill> ResolveSkillAsync(Skill skill, string appendedArguments, IFeatureActor actor) =>
        ResolveUserSkillAsync(skill, appendedArguments, actor);

    async Task<ResolvedSkill?> ResolveBuiltInSkillAsync(string builtInName, string appendedArguments, IFeatureActor actor)
    {
        var resolved = _builtinSkillRegistry[builtInName];
        if (resolved?.FeatureFlag is { Length: > 0 } && !await _featureService.IsEnabledAsync(resolved.FeatureFlag, actor))
        {
            return null;
        }

        return resolved is not null
            ? new ResolvedSkill(builtInName, appendedArguments, resolved.Description, resolved.Skill)
            : null;
    }

    async Task<ResolvedSkill> ResolveUserSkillAsync(Skill skill, string appendedArguments, IFeatureActor actor)
    {
        var userCallSkill = await ResolveBuiltInSkillAsync(
            RemoteSkillCallSkill.SkillName,
            skill.Name.AppendIfNotEmpty(appendedArguments),
            actor);
        if (userCallSkill is null)
        {
            throw new InvalidOperationException(
                $"The `{RemoteSkillCallSkill.SkillName}` skill is not present. This is a developer issue.");
        }

        return userCallSkill.WithSkill(skill);
    }
}
