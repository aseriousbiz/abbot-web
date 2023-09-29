using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;

namespace Serious.Abbot.Validation;

/// <summary>
/// Used to validate a skill name.
/// </summary>
public class SkillNameValidator : ISkillNameValidator
{
    static readonly HashSet<string> ReservedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "install"
    };
    readonly IBuiltinSkillRegistry _builtinSkillRegistry;
    readonly ISkillRepository _skillRepository;
    readonly IListRepository _userListRepository;
    readonly IAliasRepository _aliasRepository;

    public SkillNameValidator(
        IBuiltinSkillRegistry builtinSkillRegistry,
        ISkillRepository skillRepository,
        IListRepository userListRepository,
        IAliasRepository aliasRepository)
    {
        _builtinSkillRegistry = builtinSkillRegistry;
        _skillRepository = skillRepository;
        _userListRepository = userListRepository;
        _aliasRepository = aliasRepository;
    }

    /// <summary>
    /// Determines whether the provided name is a unique skill name across custom skills, built-in skills,
    /// list skills, and aliases.
    /// </summary>
    /// <param name="name">Name of the skill to test.</param>
    /// <param name="id">Id of the current entity.</param>
    /// <param name="type">The type of skill to test.</param>
    /// <param name="organization">The organization to check.</param>
    /// <returns>True if the name is unique or matches the name of the current entity specified by id and type.</returns>
    public async Task<UniqueNameResult> IsUniqueNameAsync(string name, int id, string type, Organization organization)
    {
        if (_builtinSkillRegistry[name] is not null)
        {
            return UniqueNameResult.Conflict(nameof(ISkill));
        }

        if (ReservedKeywords.Contains(name))
        {
            return UniqueNameResult.Conflict("Reserved");
        }

        var skillResult = await IsUnique(name, organization, id, type, _skillRepository, nameof(Skill));
        if (!skillResult.IsUnique)
            return skillResult;

        var listResult = await IsUnique(name, organization, id, type, _userListRepository, nameof(UserList));
        if (!listResult.IsUnique)
            return listResult;
        var aliasResult = await IsUnique(name, organization, id, type, _aliasRepository, nameof(Alias));

        return !aliasResult.IsUnique ? aliasResult : UniqueNameResult.Unique;
    }

    static async Task<UniqueNameResult> IsUnique<TEntity>(
        string name,
        Organization organization,
        int id,
        string type,
        IOrganizationScopedNamedEntityRepository<TEntity> repository,
        string existingType) where TEntity
        : class, INamedEntity, ITrackedEntity, IOrganizationEntity
    {
        var existing = await repository.GetAsync(name, organization);
        var unique = existing is null || existing.Id == id && type.Equals(existingType, StringComparison.Ordinal);
        return unique ? UniqueNameResult.Unique : UniqueNameResult.Conflict(existingType);
    }
}
