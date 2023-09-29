using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Skills.Patterns;
using Serious.Abbot.Routing;
using Serious.Abbot.Validation;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Service used to test a message against a pattern as well as all patterns in the organization.
/// </summary>
[Route("api/internal/patterns")]
public class PatternsController : InternalApiControllerBase
{
    public static readonly DomId PatternTestResultsId = new("pattern-test-results");
    readonly ISkillPatternMatcher _patternMatcher;
    readonly IPatternValidator _patternValidator;

    public PatternsController(ISkillPatternMatcher patternMatcher, IPatternValidator patternValidator)
    {
        _patternMatcher = patternMatcher;
        _patternValidator = patternValidator;
    }

    /// <summary>
    /// Test the supplied message against the supplied pattern. Also return results for any other patterns
    /// that happen to match the message.
    /// </summary>
    /// <param name="input">The form used to test the pattern.</param>
    /// <returns>A partial view with the results.</returns>
    [HttpPost("test")]
    public async Task<IActionResult> TestAsync([FromForm] PatternTestInputModel input)
    {
        // Test message against the incoming pattern.
        var skillPattern = new SkillPattern
        {
            Pattern = input.Pattern,
            PatternType = input.PatternType,
            CaseSensitive = input.CaseSensitive
        };

        var matchesPattern = skillPattern.Match(input);
        var matchingPatterns = await _patternMatcher.GetMatchingPatternsAsync(
            input,
            CurrentMember,
            Organization);

        var patternsExceptCurrent = matchingPatterns.Where(p => p.Id != input.Id);
        var model = new PatternTestResults(matchesPattern, patternsExceptCurrent);
        return TurboUpdate(PatternTestResultsId, "_PatternTestResults", model);
    }

    /// <summary>
    /// Validates that a pattern name is unique for the skill.
    /// </summary>
    /// <param name="name">Name of the pattern to test.</param>
    /// <param name="id">Id of the current entity.</param>
    /// <param name="skill">The name of the skill the pattern belongs to.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("validate/name")]
    public async Task<IActionResult> ValidateNameAsync(string name, int id, string skill)
    {
        var organization = HttpContext.GetCurrentOrganization();
        if (organization is null)
        {
            return NotFound();
        }
        var result = await _patternValidator.IsUniqueNameAsync(name, id, skill, organization);

        return result
            ? Json(true)
            : Json($"The name \"{name}\" conflicts with another pattern for this skill with the same name.");
    }

    /// <summary>
    /// Validates that a pattern name is unique for the skill.
    /// </summary>
    /// <param name="pattern">The pattern to validate.</param>
    /// <param name="patternType">The type of pattern to validate.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("validate/pattern")]
    public IActionResult ValidatePattern(string pattern, PatternType patternType)
    {
        var result = _patternValidator.IsValidPattern(pattern, patternType);

        return result
            ? Json(true)
            : Json($"The pattern <code>{HtmlEncoder.Default.Encode(pattern)}</code> is not a valid regular expression.");
    }
}
