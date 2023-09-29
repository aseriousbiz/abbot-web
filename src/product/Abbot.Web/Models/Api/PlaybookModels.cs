using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;

namespace Serious.Abbot.Models.Api;

public record PlaybookResponseModel
{
    /// <summary>
    /// A <see cref="PlaybookModel"/> describing the playbook.
    /// </summary>
    public required PlaybookModel Playbook { get; init; }

    /// <summary>
    /// A <see cref="PlaybookVersionModel"/> describing the latest version of the playbook, if any.
    /// If this is <c>null</c>, there are no versions of the playbook.
    /// </summary>
    public required PlaybookVersionModel? LatestVersion { get; init; }
}

public record CreateVersionRequestModel
{
    /// <summary>
    /// Gets or sets a description of the changes in this version.
    /// </summary>
    public string? Comment { get; init; }

    /// <inheritdoc cref="PlaybookVersion.PublishedAt"/>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookDefinition"/> that describes this version of the playbook.
    /// </summary>
    public required PlaybookDefinition Definition { get; init; }
}

public record PlaybookVersionModel
{
    /// <summary>
    /// Gets or sets the version number of this playbook version.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Gets or sets a description of the changes in this version.
    /// </summary>
    public string? Comment { get; set; }

    /// <inheritdoc cref="PlaybookVersion.PublishedAt"/>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="PlaybookDefinition"/> that describes this version of the playbook.
    /// </summary>
    public required PlaybookDefinition Definition { get; init; }
}

public record PlaybookModel
{
    /// <summary>
    /// The name of the playbook
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The slug for the playbook.
    /// This is the short name used in URLs.
    /// It is case-insensitive and can only contain letters, numbers, and hyphens.
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// A description of the playbook
    /// </summary>
    public string? Description { get; set; }
}
