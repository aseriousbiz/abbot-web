using System.Collections.Generic;

namespace Serious.Abbot.Entities;

public class PackageVersion : EntityBase<PackageVersion>
{
    /// <summary>
    /// The release notes for this release of the package.
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// The major version of the package. This increments when there's a breaking change.
    /// </summary>
    public int MajorVersion { get; set; } = 1;

    /// <summary>
    /// The minor version of the package. This increments when there's new functionality with no breaking changes.
    /// </summary>
    public int MinorVersion { get; set; }

    /// <summary>
    /// The patch version of the package. This increments when there's only bug fixes.
    /// </summary>
    public int PatchVersion { get; set; }

    /// <summary>
    /// The <see cref="Member"/> that created this version of the package.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// The Id of the <see cref="Member"/> that created this version of the package.
    /// </summary>
    public int CreatorId { get; set; }

    /// <summary>
    /// The package this version belongs to.
    /// </summary>
    public Package Package { get; set; } = null!;

    /// <summary>
    /// The id of the package this version belongs to.
    /// </summary>
    public int PackageId { get; set; }

    /// <summary>
    /// The set of skills installed from this package version.
    /// </summary>
    public List<Skill> InstalledSkills { get; set; } = new();

    /// <summary>
    /// CacheKey for the code in this skill package version. Essentially a checksum so we can quickly
    /// determine if the code has changed.
    /// </summary>
    public string CodeCacheKey { get; set; } = null!;
}
