using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Metadata;

namespace Serious.Abbot.Entities;

/// <summary>
/// Base class for skill entities such as <see cref="Skill"/>,
/// <see cref="UserList"/>, and <see cref="Alias"/>.
/// </summary>
public abstract class SkillEntityBase<TEntity> : TrackedEntityBase<TEntity>, IOrganizationEntity, INamedEntity, ISkillDescriptor
    where TEntity : class, IEntity<TEntity>
{
    /// <summary>
    /// The name of the skill.
    /// </summary>
    [Column(TypeName = "citext")]
    public string Name { get; set; } = null!;

    /// <summary>
    /// A description of the skill.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The Id of the <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public Organization Organization { get; set; } = null!;
}
