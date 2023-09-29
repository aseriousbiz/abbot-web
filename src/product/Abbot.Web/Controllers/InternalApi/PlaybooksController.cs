using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Web;

namespace Serious.Abbot.Controllers.InternalApi;

[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
[Route("api/internal/playbooks")]
[FeatureGate(FeatureFlags.Playbooks)]
public class PlaybooksController : InternalApiControllerBase
{
    /// <summary>
    /// Fetches the <see cref="Playbook"/>, including the most recent <see cref="PlaybookVersion"/>, with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the <see cref="Playbook"/> to add a version to</param>
    /// <returns>A <see cref="PlaybookVersionModel"/> describing the new version, or a <see cref="ProblemDetails"/> describing an error.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PlaybookResponseModel), StatusCodes.Status200OK)]
    // ReSharper disable once RouteTemplates.ParameterTypeAndConstraintsMismatch
    public async Task<IActionResult> GetAsync(Id<Playbook> id)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates a new version of the <see cref="Playbook"/> with the specified ID.
    /// </summary>
    /// <param name="id">The ID of the <see cref="Playbook"/> to add a version to</param>
    /// <param name="createVersionModel">A <see cref="CreateVersionRequestModel"/> describing the new version to create.</param>
    /// <returns>A <see cref="PlaybookVersionModel"/> describing the new version, or a <see cref="ProblemDetails"/> describing an error.</returns>
    [HttpPut("{id:int}/versions")]
    [ProducesResponseType(typeof(PlaybookVersionModel), StatusCodes.Status200OK)]
    // ReSharper disable once RouteTemplates.ParameterTypeAndConstraintsMismatch
    public async Task<IActionResult> CreateVersionAsync(Id<Playbook> id, [FromBody] CreateVersionRequestModel createVersionModel)
    {
        throw new NotImplementedException();
    }
}
