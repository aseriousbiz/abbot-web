namespace Serious.Abbot.Entities;

public class OrganizationEntityBase<TEntity> : EntityBase<TEntity>, IOrganizationEntity
    where TEntity : class, IEntity<TEntity>
{
    /// <summary>
    /// The Id of the <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    public static implicit operator Id<Organization>(OrganizationEntityBase<TEntity> entity) =>
        (Id<Organization>)entity.OrganizationId;
}

public class TrackedOrganizationEntityBase<TEntity> : TrackedEntityBase<TEntity>, IOrganizationEntity
    where TEntity : class, IEntity<TEntity>
{
    /// <summary>
    /// The Id of the <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    public static implicit operator Id<Organization>(TrackedOrganizationEntityBase<TEntity> entity) =>
        (Id<Organization>)entity.OrganizationId;
}
