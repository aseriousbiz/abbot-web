using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Repository used to manage skills in the database.
/// </summary>
public interface ISkillRepository : IOrganizationScopedNamedEntityRepository<Skill>
{
    /// <summary>
    /// Applies the changes in the <see cref="SkillUpdateModel"/> to the <see cref="Skill"/> and
    /// saves the changes. It creates a version snapshot (<see cref="SkillVersion"/> of the current
    /// version before applying changes.
    /// </summary>
    /// <param name="original">The original skill before the changes have been applied.</param>
    /// <param name="updateModel">The changes to apply to the skill.</param>
    /// <param name="modifiedBy">The user modifying the skill.</param>
    /// <returns><c>true</c> if there are changes and they are saved successfully. Otherwise <c>false</c>.</returns>
    Task<bool> UpdateAsync(Skill original, SkillUpdateModel updateModel, User modifiedBy);

    /// <summary>
    /// Retrieves a <see cref="Skill"/> by Id with <see cref="Skill.Data"/>
    /// populated with the skill data.
    /// </summary>
    /// <remarks>Intended to be used by the user skill data API. The skill Id is already verified
    /// for the user making the request.</remarks>
    /// <param name="skillId">The Id of the skill.</param>
    Task<Skill?> GetWithDataAsync(Id<Skill> skillId);

    /// <summary>
    /// Retrieves a <see cref="Skill"/> by Id with <see cref="Skill.Data"/>
    /// populated with the skill data.
    /// </summary>
    /// <remarks>Intended to be used by the user skill data API. The skill Id is already verified
    /// for the user making the request.</remarks>
    /// <param name="skillId">The Id of the skill.</param>
    /// <param name="scope"></param>
    /// <param name="contextId"></param>
    Task<Skill?> GetWithDataAsync(Id<Skill> skillId, SkillDataScope scope, string? contextId);

    /// <summary>
    /// Retrieves a <see cref="Skill" /> by Id. Used when invoking a skill via an interactive event.
    /// </summary>
    /// <param name="id">The id of the skill.</param>
    Task<Skill?> GetByIdAsync(Id<Skill> id);

    /// <summary>
    /// Retrieve a queryable for the skill list and skill detail page.
    /// </summary>
    /// <param name="organization">The organization the skill belongs to.</param>
    IQueryable<Skill> GetSkillListQueryable(Organization organization);

    /// <summary>
    /// Searches skills that match the query.
    /// </summary>
    /// <param name="query">The text to search for.</param>
    /// <param name="currentValue">The current value, if any.</param>
    /// <param name="limit">The number of results to return.</param>
    /// <param name="organization">The organization.</param>
    /// <returns></returns>
    Task<IReadOnlyList<Skill>> SearchAsync(string? query, string? currentValue, int limit, Organization organization);

    /// <summary>
    /// Changes the enabled/disabled state of the skill.
    /// </summary>
    /// <param name="skill">The skill to toggle.</param>
    /// <param name="enabled">Whether or not the skill is enabled.</param>
    /// <param name="actor">The user making the change.</param>
    Task ToggleEnabledAsync(Skill skill, bool enabled, User actor);

    /// <summary>
    /// Retrieves a <see cref="Skill"/> by Id with <see cref="Skill.Versions"/>
    /// populated with every version snapshot for the skill.
    /// </summary>
    /// <param name="name">The name of the skill</param>
    /// <param name="organization">The organization the skill belongs to.</param>
    Task<Skill?> GetWithVersionsAsync(string name, Organization organization);

    /// <summary>
    /// Retrieve the <see cref="SkillData"/> for the skill by key.
    /// </summary>
    /// <param name="skillId">The Id of the skill.</param>
    /// <param name="key">The data key.</param>
    Task<SkillData?> GetDataAsync(Id<Skill> skillId, string key);

    /// <summary>
    /// Retrieve the <see cref="SkillData"/> for the skill by key.
    /// </summary>
    /// <param name="skillId">The Id of the skill.</param>
    /// <param name="key">The data key.</param>
    /// <param name="scope"></param>
    /// <param name="contextId"></param>
    Task<SkillData?> GetDataAsync(Id<Skill> skillId, string key, SkillDataScope scope, string? contextId);

    /// <summary>
    /// Adds a <see cref="SkillData"/> for the skill.
    /// </summary>
    /// <param name="data">The data item to add.</param>
    Task AddDataAsync(SkillData data);

    /// <summary>
    /// Removes a <see cref="SkillData"/> for the skill.
    /// </summary>
    /// <param name="data">The data item to add.</param>
    Task DeleteDataAsync(SkillData data);

    /// <summary>
    /// Saves all changes. We should remove this.
    /// </summary>
    Task SaveChangesAsync();

    /// <summary>
    /// Adds a new exemplar to the skill.
    /// </summary>
    /// <param name="skill">The <see cref="Skill"/> to add the exemplar to.</param>
    /// <param name="text">The exemplar text to add.</param>
    /// <param name="properties">The <see cref="ExemplarProperties"/> to associate with the exemplar.</param>
    /// <param name="actor">The <see cref="Member"/> performing the action.</param>
    /// <returns>The newly created <see cref="SkillExemplar"/>, or an error if one occurred.</returns>
    Task<EntityResult<SkillExemplar>> AddExemplarAsync(Skill skill, string text, ExemplarProperties properties, Member actor);

    /// <summary>
    /// Updates an existing skill exemplar
    /// </summary>
    /// <param name="exemplar">The <see cref="SkillExemplar"/> to update.</param>
    /// <param name="text">The new text for the exemplar.</param>
    /// <param name="properties">The new <see cref="ExemplarProperties"/> to associate with the exemplar.</param>
    /// <param name="actor">The <see cref="Member"/> performing the action.</param>
    /// <returns>The updated <see cref="SkillExemplar"/>, or an error if one occurred.</returns>
    Task<EntityResult<SkillExemplar>> UpdateExemplarAsync(SkillExemplar exemplar, string text, ExemplarProperties properties, Member actor);

    /// <summary>
    /// Removes a skill exemplar.
    /// </summary>
    /// <param name="exemplar">The <see cref="SkillExemplar"/> to remove.</param>
    /// <param name="actor">The <see cref="Member"/> performing the action.</param>
    /// <returns>An <see cref="EntityResult"/> indicating success or failure.</returns>
    Task<EntityResult> RemoveExemplarAsync(SkillExemplar exemplar, Member actor);
}
