using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Skills;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

namespace Serious.Abbot.Pages.Packages;

public class DetailsModel : CustomSkillPageModel
{
    readonly IPackageRepository _packageRepository;
    readonly IPermissionRepository _permissions;

    public DetailsModel(
        IPackageRepository packageRepository,
        IPermissionRepository permissions)
    {
        _packageRepository = packageRepository;
        _permissions = permissions;
    }

    public PackageDetailsViewModel Package { get; private set; } = null!;

    // Only true if user can edit skill.
    public bool CanEditPackage { get; private set; }

    public bool OrgOwnsPackage { get; private set; }

    public Skill Skill { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string organization, string name)
    {
        var package = await _packageRepository.GetDetailsAsync(organization, name);
        if (package is null)
        {
            return NotFound();
        }

        string botName = "abbot";

        if (User.IsMember())
        {
            var currentMember = Viewer;
            botName = currentMember.Organization.GetBotName();
            OrgOwnsPackage = currentMember.OrganizationId == package.OrganizationId;

            if (OrgOwnsPackage)
            {
                Skill = package.Skill;
                CanEditPackage = await _permissions.CanEditAsync(currentMember, package.Skill);
            }
        }

        Package = new PackageDetailsViewModel(package, botName);
        return Page();
    }
}
