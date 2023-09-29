using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Models;

public class PackageUpdateModel : PackageCreateModel
{
    [Display(Name = "Change Type",
        Description = "Describe the type of change for this new version of the package. This helps users know what to expect and affects the package version.")]
    public string ChangeType { get; set; } = null!;
}
