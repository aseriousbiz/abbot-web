using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A packaged up installable <see cref="Skill"/>.
/// </summary>
public class Package : TrackedEntityBase<Package>, IOrganizationEntity, IAuditableEntity
{
    /// <summary>
    /// The programming language of the skill.
    /// </summary>
    public CodeLanguage Language { get; set; }

    /// <summary>
    /// The executable code for the skill.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// A description of the skill.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Details and instructions about the package such as how to install the package.
    /// </summary>
    public string Readme { get; set; } = string.Empty;

    /// <summary>
    /// Examples of how to use the skill created by a package.
    /// </summary>
    public string UsageText { get; set; } = string.Empty;

    /// <summary>
    /// The Id of the organization that owns the organization.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The organization that owns the package.
    /// </summary>
    public Organization Organization { get; set; } = null!;

    /// <summary>
    /// The source skill this packages.
    /// </summary>
    public Skill Skill { get; set; } = null!;

    /// <summary>
    /// The Id of the source skill this packages.
    /// </summary>
    public int SkillId { get; set; }

    /// <summary>
    /// Retrieves the version history of this package.
    /// </summary>
    public List<PackageVersion> Versions { get; set; } = new();

    /// <summary>
    /// Whether the package is listed in the directory and search results.
    /// </summary>
    public bool Listed { get; set; } = true;

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var verb = auditOperation switch
        {
            AuditOperation.Created => "Published",
            AuditOperation.Removed => "Unlisted",
            AuditOperation.Changed => "Changed",
            _ => throw new InvalidOperationException($"Unknown {nameof(AuditOperation)} {auditOperation}.")
        };

        var packageVersion = Versions.LastOrDefault();

        var packageEvent = new PackageEvent
        {
            SkillId = Skill.Id,
            SkillName = Skill.Name,
            Language = Skill.Language,
            Description = $"{verb} package `{Skill.Name}` version `{this.GetLatestVersion().ToVersionString()}`.",
            PackageVersionId = packageVersion?.Id,
            ReleaseNotes = packageVersion?.ReleaseNotes
        };

        if (auditOperation is AuditOperation.Created)
        {
            packageEvent.Readme = Readme;
        }

        return packageEvent;
    }
}
