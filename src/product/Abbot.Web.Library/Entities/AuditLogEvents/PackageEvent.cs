using System.ComponentModel.DataAnnotations.Schema;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents an Activity log item when a package is published, unlisted, and edited.
/// </summary>
public class PackageEvent : SkillAuditEvent
{
    /// <summary>
    /// The new readme, if it's changed.
    /// </summary>
    [Column("Arguments")]  // Repurposing the column of a sibling hierarchy.
    public string? Readme { get; set; }

    /// <summary>
    /// The release notes for this package version.
    /// </summary>
    [Column("Reason")] // Reusing existing column.
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// The id of the specific package version this is associated with.
    /// </summary>
    [Column("FirstSkillVersionId")] // Reusing existing column.
    public int? PackageVersionId { get; set; }

    [NotMapped]
    public override bool HasDetails => true;
}
