namespace Serious.Abbot.Entities;

/// <summary>
/// An entity that belongs to an organization.
/// </summary>
public interface IOrganizationEntity : IEntity
{
    /// <summary>
    /// The Id of the <see cref="Organization"/> this entity belongs to.
    /// </summary>
    int OrganizationId { get; }

    /// <summary>
    /// The <see cref="Organization"/> this entity belongs to.
    /// </summary>
    Organization Organization { get; }
}
