using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;
using Serious.Abbot.Web;

namespace Serious.Abbot.Controllers.InternalApi;

[Authorize(Policy = AuthorizationPolicies.RequireAuthenticated)]
[Route("api/internal/stepTypes")]
[FeatureGate(FeatureFlags.Playbooks)]
public class StepTypesController : InternalApiControllerBase
{
    readonly StepTypeCatalog _stepTypeCatalog;
    readonly FeatureService _featureService;
    readonly IIntegrationRepository _integrationRepository;

    public StepTypesController(StepTypeCatalog stepTypeCatalog, FeatureService featureService, IIntegrationRepository integrationRepository)
    {
        _stepTypeCatalog = stepTypeCatalog;
        _featureService = featureService;
        _integrationRepository = integrationRepository;
    }

    /// <summary>
    /// Fetches all the <see cref="StepType"/>s known to the system.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(StepTypeList), StatusCodes.Status200OK)]
    // ReSharper disable once RouteTemplates.ParameterTypeAndConstraintsMismatch
    public async Task<IActionResult> GetAllAsync()
    {
        var allTypes = await _stepTypeCatalog.GetAllTypesAsync(Organization, HttpContext.GetFeatureActor());
        return Ok(allTypes);
    }

    /// <summary>
    /// Fetches the <see cref="StepType"/> with the specified name.
    /// </summary>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(StepType), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(string name)
    {
        if (_stepTypeCatalog.TryGetType(name, out var t))
        {
            return Ok(t);
        }

        return Problem(Problems.NotFound("Step type not found.", $"Could not find a step type with the name '{name}'"));
    }
}
