namespace Serious.Abbot.Entities;

/// <summary>
/// An entity that can be recovered by staff when deleted.
/// When deleted, the <see cref="IsDeleted"/> property is set to true.
/// </summary>
public interface IRecoverableEntity
{
    /// <summary>
    /// If true, the entity is "soft" deleted and does not show up in any queries.
    /// </summary>
    bool IsDeleted { get; set; }
}
