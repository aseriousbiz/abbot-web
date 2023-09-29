using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers;

public class MetadataController : SkillRunnerApiControllerBase
{
    readonly IMetadataRepository _metadataRepository;

    public MetadataController(IMetadataRepository metadataRepository)
    {
        _metadataRepository = metadataRepository;
    }

    /// <summary>
    /// Retrieves all the metadata field defined for the organization.
    /// </summary>
    [HttpGet("metadata")]
    public async Task<IActionResult> GetAllAsync()
    {
        var metadata = await _metadataRepository.GetAllAsync(MetadataFieldType.Room, Organization);
        return Ok(metadata.Select(field => field.ToMetadataFieldInfo()).ToList());
    }

    /// <summary>
    /// Retrieves all the metadata field defined for the organization.
    /// </summary>
    [HttpGet("metadata/{name}")]
    public async Task<IActionResult> GetByNameAsync(string name)
    {
        var metadata = await _metadataRepository.GetByNameAsync(MetadataFieldType.Room, name, Organization);
        if (metadata is null)
        {
            return NotFound();
        }
        return Ok(metadata.ToMetadataFieldInfo());
    }

    /// <summary>
    /// Creates a new metadata field for the organization
    /// </summary>
    [HttpPost("metadata")]
    public async Task<IActionResult> PostAsync(MetadataFieldInfo metadataFieldInfo)
    {
        var existing = await _metadataRepository.GetByNameAsync(MetadataFieldType.Room, metadataFieldInfo.Name, Organization);
        if (existing is not null)
        {
            return Problem("A metadata field with that name already exists.", statusCode: 409);
        }

        await _metadataRepository.CreateMetadataFieldAsync(
            MetadataFieldType.Room,
            metadataFieldInfo.Name,
            metadataFieldInfo.DefaultValue,
            Member,
            Organization);
        return Ok(metadataFieldInfo);
    }

    /// <summary>
    /// Gets a new metadata field for the organization
    /// </summary>
    [HttpPut("metadata/{name}")]
    public async Task<IActionResult> PutAsync(string name, MetadataFieldInfo metadataFieldInfo)
    {
        var result = await _metadataRepository.UpdateMetadataFieldAsync(
            MetadataFieldType.Room,
            name,
            metadataFieldInfo.Name,
            metadataFieldInfo.DefaultValue,
            Member,
            Organization);

        if (result is null)
        {
            return NotFound();
        }
        return Ok(result.ToMetadataFieldInfo());
    }
}

public static class MetadataFieldExtensions
{
    [return: NotNullIfNotNull(nameof(metadataField))]
    public static MetadataFieldInfo? ToMetadataFieldInfo(this MetadataField? metadataField)
    {
        return metadataField is null ? null : new MetadataFieldInfo(metadataField.Name, metadataField.DefaultValue);
    }
}

