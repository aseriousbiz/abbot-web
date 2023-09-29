using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public class PackageCreateModel
{
    [Display(Description = "Provides users of the package information on the package and how to set it up. Markdown is supported.")]
    public string? Readme { get; set; }

    [Display(Name = "Release Notes", Description = "Describe what's changed in this version. Get users excited to try it out. Markdown is supported.")]
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Applies the changes in this model to the supplied SkillPackage.
    /// </summary>
    /// <param name="package">The skill package to apply these changes to.</param>
    /// <param name="skill">The skill this package packages up.</param>
    /// <param name="user">The user applying these changes</param>
    public void Apply(Package package, Skill skill, User user)
    {
        package.Readme = Readme ?? package.Readme;
        package.Description = skill.Description;
        package.UsageText = skill.UsageText;
        package.Code = skill.Code;
        package.Language = skill.Language;
        package.Modified = DateTime.UtcNow;
        package.ModifiedBy = user;
    }

    public Package CreatePackageInstance(Skill skill, User user)
    {
        var package = new Package
        {
            Created = DateTime.UtcNow,
            Creator = user,
            ModifiedBy = user,
            Skill = skill,
            Versions = new List<PackageVersion>
            {
                new()
                {
                    MajorVersion = 1,
                    MinorVersion = 0,
                    PatchVersion = 0,
                    ReleaseNotes = ReleaseNotes ?? string.Empty,
                    Creator = user,
                    CodeCacheKey = SkillCompiler.ComputeCacheKey(skill.CacheKey)
                }
            },
            Organization = skill.Organization
        };
        Apply(package, skill, user);
        return package;
    }
}
