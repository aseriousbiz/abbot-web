using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.AspNetCore.ModelBinding;

namespace Serious.Abbot.Pages;

/// <summary>
/// Base class for a class that manages metadata fields for an entity.
/// </summary>
public abstract class MetadataEditorPage : UserPage
{
    protected IMetadataRepository MetadataRepository { get; }

    public DomId EntityMetadataId { get; } = new("entity-metadata");
    public DomId MetadataStatusMessage { get; } = new("entity-metadata-status");

    protected MetadataEditorPage(MetadataFieldType metadataFieldType, IMetadataRepository metadataRepository)
    {
        MetadataRepository = metadataRepository;
        MetadataFieldType = metadataFieldType;
    }

    public MetadataFieldType MetadataFieldType { get; }

    public abstract List<EntityMetadataInput> EntityMetadataInputs { get; set; }

    public IReadOnlyList<MetadataField> MetadataFields { get; protected set; } = null!;
}

public abstract class MetadataEditorPage<TEntity, TId> : MetadataEditorPage where TEntity : IEntity
{
    protected MetadataEditorPage(IMetadataRepository metadataRepository)
        : base(GetMetadataFieldType(), metadataRepository)
    {
    }

    protected async Task InitializeMetadataAsync(TEntity entity)
    {
        MetadataFields = await MetadataRepository.GetAllAsync(MetadataFieldType, Organization);
        EntityMetadataInputs = MetadataFields.Select(m => EntityMetadataInput.FromMetadataField(m, entity)).ToList();
    }

    protected abstract Task<TEntity?> InitializePageAsync(TId entityId);

    protected abstract Task<TEntity?> GetEntityAsync(TId entityId, Organization organization);

    protected abstract Task UpdateEntityMetadataAsync(TEntity entity, Dictionary<string, string?> metadataUpdates, Member actor);

    protected async Task<IActionResult> HandlePostSaveMetadataAsync(TId entityId)
    {
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            return TurboFlash("You must upgrade your plan to use this feature.");
        }

        if (!Viewer.CanManageConversations())
        {
            return TurboFlash("You do not have permission to manage conversations.");
        }

        var entity = await GetEntityAsync(entityId, Organization);
        if (entity is null)
        {
            return NotFound();
        }

        if (!ModelState.RemoveExcept(nameof(EntityMetadataInputs)).IsValid)
        {
            return TurboStream(
                TurboFlash(MetadataStatusMessage, "Some errors occurred while saving the metadata.", isError: true),
                TurboUpdate(EntityMetadataId, "Shared/Metadata/_EntityMetadataEditor", this));
        }

        var metadataUpdates = EntityMetadataInputs.ToDictionary(i => i.Name, i => i.Value);
        await UpdateEntityMetadataAsync(entity, metadataUpdates, Viewer);

        await InitializePageAsync(entityId);

        return TurboStream(
            TurboFlash(MetadataStatusMessage, "Metadata changes saved."),
            TurboUpdate(EntityMetadataId, "Shared/Metadata/_EntityMetadataEditor", this));
    }

    static MetadataFieldType GetMetadataFieldType() =>
        typeof(TEntity) switch
        {
            { } t when t == typeof(Room) => MetadataFieldType.Room,
            { } t when t == typeof(Customer) => MetadataFieldType.Customer,
            _ => throw new InvalidOperationException($"Unknown entity type {typeof(TEntity)}")
        };
}
