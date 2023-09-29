using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Validation;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Service used when creating or editing a skill and applying validators to the input model
/// asynchronously using the <see cref="RemoteAttribute"/>.
/// </summary>
[ApiController]
[AbbotWebHost]
[Route("api/internal/skills")]
public class SkillValidationController : InternalApiControllerBase
{
    readonly ISkillNameValidator _skillNameValidator;

    public SkillValidationController(ISkillNameValidator skillNameValidator)
    {
        _skillNameValidator = skillNameValidator;
    }

    /// <summary>
    /// Validates that a name is unique for the skill and organization.
    /// </summary>
    /// <param name="name">Name of the skill to test.</param>
    /// <param name="id">Id of the current entity.</param>
    /// <param name="type">The type of skill to test.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateAsync(string name, int id, string type)
    {
        var result = await _skillNameValidator
            .IsUniqueNameAsync(name, id, type, Organization);

        return result.IsUnique
            ? Json(true)
            : Json(result.ConflictType == UniqueNameResult.ReservedKeywordConflict
                ? $"The name \"{name}\" is reserved."
                : $"The name \"{name}\" conflicts with a {result.ConflictTypeFriendlyName} with the same name.");
    }
}
