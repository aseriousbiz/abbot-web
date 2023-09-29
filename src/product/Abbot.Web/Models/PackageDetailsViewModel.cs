using System.Collections.Immutable;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public class PackageDetailsViewModel : PackageItemViewModel
{
    public PackageDetailsViewModel(Package package, string botName) : base(package)
    {
        Readme = package.Readme;
        Versions = package.Versions.OrderByDescending(v => v.Created).ToImmutableList();
        ModifiedBy = package.ModifiedBy;
        Usage = package.GetUsageText(botName);
        IsListed = package.Listed;
    }

    public string Readme { get; }

    public string Usage { get; }

    public bool IsListed { get; }

    public IImmutableList<PackageVersion> Versions { get; }

    public User ModifiedBy { get; }
}
