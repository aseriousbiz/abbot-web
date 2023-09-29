using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Humanizer;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.Entities;

/// <summary>
/// A user created skill that adds new capabilities to an Abbot instance.
/// </summary>
[DebuggerDisplay("{Name} {Id}")]
public partial class Skill : SkillEntityBase<Skill>, IRecoverableEntity, IAuditableEntity
{
    public const string NamePattern = $"{WebConstants.NameCharactersPattern}{{0,38}}";
    public const string ValidNamePattern = $"^{NamePattern}$";
    public const string NameErrorMessage =
        "Name may only contain a-z and 0-9. For multi-word names, separate the words by a dash character.";
#pragma warning disable CA1805
    public static readonly Id<Skill> SystemSkillId = default; // Default value is 0, which is, in fact, the System Skill ID.
#pragma warning restore CA1805
    public static readonly string SystemSkillName = "<system>";

    /// <summary>
    /// The programming language of the skill.
    /// </summary>
    public CodeLanguage Language { get; set; }

    /// <summary>
    /// The executable code for the skill.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// A checksum used to uniquely identify the code and determine if the code has changed. In the case of C#
    /// skills, this is the name of the assembly created.
    /// </summary>
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// Examples of how to use the skill.
    /// </summary>
    public string UsageText { get; set; } = string.Empty;

    /// <summary>
    /// Whether the skill is enabled or not.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// The data stored for the skill.
    /// </summary>
    public List<SkillData> Data { get; set; } = new();

    /// <summary>
    /// References to secrets the skill may need to use. The actual
    /// secret value is stored in Azure Key Vault.
    /// </summary>
    public List<SkillSecret> Secrets { get; set; } = new();

    /// <summary>
    /// The set of HTTP triggers that can be used to call the skill
    /// via an external system.
    /// </summary>
    public List<SkillTrigger> Triggers { get; set; } = new();

    /// <summary>
    /// The set of patterns that this skill listens to for incoming messages.
    /// </summary>
    public List<SkillPattern> Patterns { get; set; } = new();

    /// <summary>
    /// The set of signals that this skill is subscribed to.
    /// </summary>
    public List<SignalSubscription> SignalSubscriptions { get; set; } = new();

    /// <summary>
    /// The AI training exemplars configured for this skill.
    /// </summary>
    public List<SkillExemplar> Exemplars { get; set; } = new();

    /// <summary>
    /// Whether or not the skill is deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// The version history of the skill.
    /// </summary>
    public List<SkillVersion> Versions { get; set; } = new();

    /// <summary>
    /// The package this skill was installed from, if any. This gives us a means to determine if the
    /// skill is on the latest version or not.
    /// </summary>
    public PackageVersion? SourcePackageVersion { get; set; }

    /// <summary>
    /// The Id of the package this skill was installed from, if any.
    /// </summary>
    public int? SourcePackageVersionId { get; set; }

    /// <summary>
    /// The package for this skill.
    /// </summary>
    /// <remarks>
    /// Not to be confused with the source package (a package the skill was installed from). This is the
    /// <see cref="Package"/> created if this skill was published.
    /// </remarks>
    public Package? Package { get; set; }

    /// <summary>
    /// Whether or not the skill is subject to access controls or not.
    /// <c>true</c> if permissions are required, <c>false</c> if anyone can call it.
    /// </summary>
    public bool Restricted { get; set; }

    [Column(TypeName = "text")]
    public SkillDataScope Scope { get; set; } = SkillDataScope.Organization;

    /// <summary>
    /// Non-queryable properties associated with this skill.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public SkillProperties Properties { get; set; } = new();

    public AuditEventBase CreateAuditEventInstance(AuditOperation auditOperation)
    {
        var description = $"{auditOperation} {Language.Humanize()} skill `{Name}`";
        if (SourcePackageVersionId is not null)
        {
            description += " from package";
        }

        return new SkillAuditEvent
        {
            Description = description + "."
        };
    }
}

public record SkillProperties
{
    public bool? ArgumentExtractionEnabled { get; init; }
}
