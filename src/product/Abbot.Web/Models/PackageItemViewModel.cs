using System;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

/// <summary>
/// View Model for a package within the package directory listing.
/// </summary>
public class PackageItemViewModel
{
    public PackageItemViewModel(Package package)
    {
        Id = package.Id;
        Name = package.Skill.Name;
        LatestVersion = package.GetLatestVersion();
        Version = LatestVersion.ToVersionString();
        Icon = package.Organization.Avatar;
        OrganizationId = package.OrganizationId;
        OrganizationName = package.Organization.Name ?? package.Organization.Domain ?? "unknown";
        Description = package.Description;
        Language = package.Language;
        Modified = package.Modified;
        InstallCount = 0; // TODO: Calculate this.
        OrganizationSlug = package.Organization.Slug;
        TotalInstalls = package.Versions.Sum(v => v.InstalledSkills.Count);
        Author = package.Creator;
        Listed = package.Listed;
        UsageText = package.UsageText;
        Code = package.Code;
    }

    public int Id { get; }

    public User Author { get; }

    public string Name { get; }

    public string Description { get; }

    public CodeLanguage Language { get; }

    public string OrganizationName { get; }

    public string Version { get; }

    public string Icon { get; }

    public DateTime Modified { get; }

    public int InstallCount { get; }

    public string OrganizationSlug { get; }

    public int TotalInstalls { get; }

    public int OrganizationId { get; }

    public bool Listed { get; }

    public PackageVersion LatestVersion { get; }

    public string UsageText { get; }

    public string Code { get; }
}
