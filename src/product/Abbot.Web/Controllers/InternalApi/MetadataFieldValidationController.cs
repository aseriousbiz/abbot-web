using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Service used when creating metadata fields and applying validators to the input model
/// asynchronously using the <see cref="RemoteAttribute"/>.
/// </summary>
[ApiController]
[AbbotWebHost]
[Route("api/internal/metadata")]
public class MetadataFieldValidationController : InternalApiControllerBase
{
    readonly IMetadataRepository _metadataRepository;

    public MetadataFieldValidationController(IMetadataRepository metadataRepository)
    {
        _metadataRepository = metadataRepository;
    }

    /// <summary>
    /// Validates that a name is unique for the organization.
    /// </summary>
    /// <param name="name">Name of the metadata to test.</param>
    /// <param name="type">The type of metadata field.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateAsync(string name, MetadataFieldType type)
    {
        var existing = await _metadataRepository.GetByNameAsync(type, name, Organization);
        return existing is null
            ? Json(true)
            : Json($"Metadata \"{existing.Name}\" already exists for this room.");
    }
}
