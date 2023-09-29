using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Service used when creating or editing a pattern and applying validators to the input model
/// asynchronously using the <see cref="RemoteAttribute"/>.
/// </summary>
[Route("api/internal/signals")]
public class SignalValidationController : InternalApiControllerBase
{
    readonly ISignalRepository _signalRepository;

    public SignalValidationController(ISignalRepository signalRepository)
    {
        _signalRepository = signalRepository;
    }

    /// <summary>
    /// Validates that a pattern name is unique for the skill.
    /// </summary>
    /// <param name="name">Name of the signal to test.</param>
    /// <param name="skill">The name of the skill the pattern belongs to.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("name")]
    public async Task<IActionResult> ValidateNameAsync(string name, string skill)
    {
        var result = await _signalRepository.GetAsync(name, skill, Organization);

        return result is null
            ? Json(true)
            : Json($"This skill is already subscribed to the signal \"{name}\".");
    }
}
