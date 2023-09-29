using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Skills;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Razor.Components.Services;

namespace Serious.Abbot.Pages.Packages;

public class EditModel : CustomSkillPageModel
{
    readonly IPackageRepository _packageRepository;
    readonly IPermissionRepository _permissions;

    public EditModel(
        IPackageRepository packageRepository,
        IPermissionRepository permissions)
    {
        _packageRepository = packageRepository;
        _permissions = permissions;
    }

    public PackageDetailsViewModel Package { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new InputModel();

    public async Task<IActionResult> OnGetAsync(string organization, string name)
    {
        var (member, package) = await InitializePageAsync(organization, name);
        if (member is null || package is null)
        {
            return NotFound();
        }

        Input.Listed = package.Listed;
        Input.Readme = package.Readme;

        var botName = member.Organization.GetBotName();
        Package = new PackageDetailsViewModel(package, botName);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string organization, string name)
    {
        var (member, package) = await InitializePageAsync(organization, name);
        if (member is null || package is null)
        {
            return NotFound();
        }

        // Sanitize the input.
        var readme = Input.Readme is { Length: > 0 } ? MarkdownService.SanitizeHtml(Input.Readme) : string.Empty;

        await _packageRepository.UpdatePackageMetadataAsync(
            package,
            readme,
            Input.Listed,
            member.User);

        StatusMessage = "Package Updated";

        return RedirectToPage("Details", new { organization, name });
    }

    public async Task<(Member?, Package?)> InitializePageAsync(string organization, string name)
    {
        if (!User.IsMember())
        {
            return (null, null);
        }
        var member = Viewer;
        var package = await _packageRepository.GetDetailsAsync(organization, name);
        if (package is null)
        {
            return (null, null);
        }

        if (member.OrganizationId != package.OrganizationId)
        {
            return (null, null);
        }

        if (!await _permissions.CanEditAsync(member, package.Skill))
        {
            return (null, null);
        }

        Package = new PackageDetailsViewModel(package, member.Organization.GetBotName());

        return (member, package);
    }

    public class InputModel : PackageUpdateModel
    {
        [Display(Description = "Uncheck this to hide this package from search results for others. Anyone with a direct link to the package will still be able to see and install it.")]
        public bool Listed { get; set; }
    }
}
