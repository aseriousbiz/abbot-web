using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Skills;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Packages;

public class SeoDetailsModel : CustomSkillPageModel
{
    readonly IPackageRepository _packageRepository;

    public SeoDetailsModel(IPackageRepository packageRepository)
    {
        _packageRepository = packageRepository;
    }

    public PackageDetailsViewModel Package { get; private set; } = null!;
    public string Platform { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(string organization, string platform, string name)
    {
        Platform = new CultureInfo("en-US").TextInfo.ToTitleCase(platform);
        var package = await _packageRepository.GetDetailsAsync(organization, name);
        if (package is null)
        {
            return NotFound();
        }

        Package = new PackageDetailsViewModel(package, "abbot");
        return Page();
    }
}
