using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Controller responsible for handling data storage and retrieval operations from skills. This is the
/// Bot.Brain that we provide to skill authors.
/// </summary>
public class BrainController : SkillRunnerApiControllerBase
{
    readonly ISkillRepository _skillRepository;

    public BrainController(ISkillRepository skillRepository)
    {
        _skillRepository = skillRepository;
    }

    /// <summary>
    /// Retrieves data from the brain for the specified key. If no key is provided, returns all the data.
    /// </summary>
    /// <param name="key">The key of the data to retrieve</param>
    /// <param name="scope">The <see cref="SkillDataScope"/> scope of the data to query.</param>
    /// <param name="contextId">The context id matching the requested scope.</param>
    [HttpGet("brain")]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? key,
        [FromQuery] SkillDataScope? scope = null,
        [FromQuery] string? contextId = null)
    {
        if (key is null)
        {
            var skill = scope is null
                ? await _skillRepository.GetWithDataAsync(Skill)
                : await _skillRepository.GetWithDataAsync(Skill, scope.Value, contextId);

            if (skill is null)
                return NotFound();

            return new ObjectResult(skill.Data.ToDictionary(d => d.Key, d => d.Value));
        }

        var data = scope is null
            ? await _skillRepository.GetDataAsync(Skill, key)
            : await _skillRepository.GetDataAsync(Skill, key, scope.Value, contextId);

        if (data is null)
            return NotFound();

        return new ObjectResult(new SkillDataResponse
        {
            Key = data.Key,
            Value = data.Value
        });
    }

    /// <summary>
    /// Add or update a data item to the brain associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the data to retrieve</param>
    /// <param name="request">Describes the change to make</param>
    [HttpPost("brain")]
    public async Task<IActionResult> PostAsync(
        [FromQuery] string key,
        [FromBody] SkillDataUpdateRequest request)
    {
        var data = await _skillRepository.GetDataAsync(Skill, key, request.Scope, request.ContextId);
        if (data is null)
        {
            data = new SkillData
            {
                Key = key.ToLowerInvariant(),
                SkillId = Skill.Id,
                CreatorId = Member.User.Id,
                ModifiedById = Member.User.Id,
                Value = request.Value,
                Scope = request.Scope,
                ContextId = request.ContextId
            };

            await _skillRepository.AddDataAsync(data);
        }
        else
        {
            data.Value = request.Value;
            data.ModifiedById = Member.User.Id;
            await _skillRepository.SaveChangesAsync();
        }

        return new ObjectResult(new SkillDataResponse
        {
            Key = data.Key,
            Value = data.Value
        });
    }

    /// <summary>
    /// Deletes an item from the brain
    /// </summary>
    /// <param name="key">The key of the data to retrieve</param>
    /// <param name="scope"></param>
    /// <param name="contextId">The id of the user/conversation/room where the skill is running.</param>
    /// <returns></returns>
    [HttpDelete("brain")]
    public async Task<IActionResult> DeleteAsync(
        [FromQuery] string key,
        [FromQuery] SkillDataScope? scope = null,
        [FromQuery] string? contextId = null)
    {
        var data = scope is not null
            ? await _skillRepository.GetDataAsync(Skill, key, scope.Value, contextId)
            : await _skillRepository.GetDataAsync(Skill, key);

        if (data is null)
            return NotFound();

        await _skillRepository.DeleteDataAsync(data);
        return Ok();
    }
}
