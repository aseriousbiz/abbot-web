using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills;

public class UpgradeModel : CustomSkillPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;

    public UpgradeModel(
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _permissions = permissions;
    }

    public Skill Skill { get; private set; } = null!;

    public PackageDetailsViewModel Package { get; private set; } = null!;

    // These are the versions of the package since this package was installed.
    public IReadOnlyList<PackageVersion> NewPackageVersions { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var (_, _, sourcePackageVersion) = await InitializePage(skill);

        if (sourcePackageVersion is null)
        {
            return NotFound();
        }

        NewPackageVersions = sourcePackageVersion
            .Package
            .Versions
            .Where(pv => pv.Id != sourcePackageVersion.Id && pv.Created >= sourcePackageVersion.Created)
            .OrderBy(v => v.Created).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill)
    {
        var (user, dbSkill, sourcePackageVersion) = await InitializePage(skill);

        if (dbSkill is null || sourcePackageVersion is null || user is null)
        {
            return NotFound();
        }

        var updateModel = new SkillUpdateModel
        {
            Code = Package.Code,
            UsageText = Package.Usage,
            Description = Package.Description
        };
        dbSkill.SourcePackageVersionId = sourcePackageVersion.Package.GetLatestVersion().Id;

        await _skillRepository.UpdateAsync(dbSkill, updateModel, user);

        StatusMessage = "Skill updated to latest version of the package.";
        return Redirect($"/skills/{skill}");
    }

    async Task<(User?, Skill?, PackageVersion?)> InitializePage(string skill)
    {
        var currentMember = Viewer;
        var (user, organization) = currentMember;
        var dbSkill = await _skillRepository.GetAsync(skill, organization);
        if (dbSkill is null)
        {
            return (null, null, null);
        }

        if (!await _permissions.CanEditAsync(currentMember, dbSkill))
        {
            return (null, null, null);
        }

        var sourcePackageVersion = dbSkill.SourcePackageVersion;

        if (sourcePackageVersion is null)
        {
            return (null, null, null);
        }

        Skill = dbSkill;
        var botName = currentMember.Organization.GetBotName();
        Package = new PackageDetailsViewModel(sourcePackageVersion.Package, botName);

        return (user, dbSkill, sourcePackageVersion);
    }
}
