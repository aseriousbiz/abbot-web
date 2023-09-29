using System;

namespace Serious.Abbot.Entities;

/// <summary>
/// An entity that stores the <see cref="User"/> that created and modified it.
/// </summary>
public interface ITrackedEntity : IEntity
{
    /// <summary>
    /// The <see cref="User"/> that created this entity.
    /// </summary>
    public User Creator { get; set; }

    /// <summary>
    /// The date (UTC) when this entity was last modified.
    /// </summary>
    // This is a DateTime because there's no Postgres type that stores date with offset.
    public DateTime Modified { get; set; }

    /// <summary>
    /// The <see cref="User"/> that last modified this entity.
    /// </summary>
    public User ModifiedBy { get; set; }

    /// <summary>
    /// The Id of the <see cref="User"/> that last modified this entity.
    /// </summary>
    public int ModifiedById { get; set; }
}
