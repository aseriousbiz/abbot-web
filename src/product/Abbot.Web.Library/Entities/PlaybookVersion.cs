using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a version of a playbook.
/// </summary>
public class PlaybookVersion : TrackedEntityBase<PlaybookVersion>
{
    /// <summary>
    /// Gets or inits the ID of the <see cref="Entities.Playbook"/> this version belongs to.
    /// </summary>
    public int PlaybookId { get; init; }

    /// <summary>
    /// Gets or inits the <see cref="Entities.Playbook"/> this version belongs to.
    /// </summary>
    public required Playbook Playbook { get; init; }

    /// <summary>
    /// Gets or sets the version number of this playbook.
    /// </summary>
    /// <remarks>
    /// Playbook version numbers should be sequential and have no gaps.
    /// The first version of a playbook should be 1.
    /// For any playbook version X, the version X-1 should exist and describe the immediately-preceding version.
    /// </remarks>
    public required int Version { get; init; }

    /// <summary>
    /// Gets or sets a description of the changes in this version.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    /// Gets or sets a timestamp indicating when this version of the playbook was published.
    /// Whenever a playbook is executed, the latest published version is used.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the serialized <see cref="PlaybookDefinition"/> that describes this version of the playbook.
    /// Deserialize this with <see cref="PlaybookFormat"/>.
    /// </summary>
    [Column("Definition", TypeName = "jsonb")]
    public required string SerializedDefinition { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookVersionProperties"/> representing additional properties of this version of the playbook.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public PlaybookVersionProperties Properties { get; set; } = new();
}

/// <summary>
/// Represents additional properties of a playbook version.
/// </summary>
public record PlaybookVersionProperties;
