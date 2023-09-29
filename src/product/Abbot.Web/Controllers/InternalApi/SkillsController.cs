using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Controllers.InternalApi;

[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
[Route("api/internal/skills")]
[FeatureGate(FeatureFlags.Playbooks)]
public class SkillsController : InternalApiControllerBase
{
    readonly ISkillRepository _skillRepository;

    public SkillsController(ISkillRepository skillRepository)
    {
        _skillRepository = skillRepository;
    }

    /// <summary>
    /// Fetches all the <see cref="SkillSummary"/>s for the current organization.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SkillSummary>>> GetAllAsync()
    {
        var skills = await _skillRepository.GetAllAsync(Organization);
        return skills.Select(SkillSummary.FromSkill).OrderBy(s => s.Name).ToList();
    }

    /// <summary>
    /// Returns all skills matching <paramref name="q"/>.
    /// </summary>
    /// <param name="q">The substring to request.</param>
    /// <param name="currentValue">The current value we want to ensure is in the set of options even with no query results.</param>
    /// <param name="limit">The number of results to return.</param>
    [HttpGet("typeahead")]
    public async Task<IActionResult> GetMatchingSkillsAsync([FromQuery] string? q, [FromQuery] string? currentValue, [FromQuery] int limit = 10)
    {
        if (!Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            return Problem("No API token configured.");
        }

        var results = await _skillRepository.SearchAsync(q, currentValue, limit, Organization);
        return Json(results.Select(TypeAheadResponseModel.Create).ToList());
    }

    public record SkillSummary(string Name, bool Enabled)
    {
        public static SkillSummary FromSkill(Skill skill) =>
            new(skill.Name, skill.Enabled);
    }
}
