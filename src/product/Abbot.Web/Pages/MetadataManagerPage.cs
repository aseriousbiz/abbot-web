using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.AspNetCore.ModelBinding;

namespace Serious.Abbot.Pages;

/// <summary>
/// Base class for a page that manages metadata fields for an organization.
/// </summary>
public abstract class MetadataManagerPage : UserPage
{
    const string PartialName = "Shared/Metadata/_MetadataFields";

    public DomId MetadataFieldsId { get; } = new("metadata-fields");

    readonly IMetadataRepository _metadataRepository;

    protected MetadataManagerPage(IMetadataRepository metadataRepository, MetadataFieldType metadataFieldType)
    {
        _metadataRepository = metadataRepository;
        MetadataFieldType = metadataFieldType;
    }

    public MetadataFieldType MetadataFieldType { get; }

    public IReadOnlyList<MetadataField> MetadataFields { get; set; } = null!;

    public abstract string? MetadataFieldToDelete { get; set; }

    public abstract MetadataFieldInput MetadataInput { get; set; }

    protected async Task InitializeMetadataAsync()
    {
        MetadataFields = await _metadataRepository.GetAllAsync(MetadataFieldType, Organization);
    }

    public async Task<IActionResult> OnPostAddMetadataFieldAsync(string roomId)
    {
        MetadataFields = await _metadataRepository.GetAllAsync(MetadataFieldType, Organization);

        ModelState.RemoveExcept(nameof(MetadataInput));

        if (MetadataInput is { } input && ModelState.IsValid)
        {
            await _metadataRepository.CreateMetadataFieldAsync(
                MetadataFieldType,
                input.Name,
                input.DefaultValue,
                Viewer,
                Organization);

            // Clear the input fields.
            ModelState.Clear();
            MetadataInput = new MetadataFieldInput
            {
                Name = string.Empty,
                DefaultValue = null,
            };

            MetadataFields = await _metadataRepository.GetAllAsync(MetadataFieldType, Organization);

            return TurboStream(
                TurboFlash("Metadata field added!"),
                TurboUpdate(MetadataFieldsId, PartialName, this));
        }

        return TurboStream(
            TurboFlash("Invalid metadata field", isError: true),
            TurboUpdate(MetadataFieldsId, PartialName, this));
    }

    public async Task<IActionResult> OnPostDeleteMetadataFieldAsync()
    {
        var metadataField = await _metadataRepository.GetByNameAsync(
            MetadataFieldType,
            MetadataFieldToDelete.Require(),
            Organization);
        if (metadataField is null)
        {
            return NotFound();
        }

        await _metadataRepository.RemoveAsync(metadataField, Viewer.User);

        MetadataFields = await _metadataRepository.GetAllAsync(MetadataFieldType, Organization);
        return TurboStream(
            TurboFlash($"Metadata \"{MetadataFieldToDelete}\" deleted."),
            TurboUpdate(MetadataFieldsId, PartialName, this));
    }
}
