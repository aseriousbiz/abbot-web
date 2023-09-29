using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Skills;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Collections;

namespace Serious.Abbot.Pages.Packages;

public class IndexModel : CustomSkillPageModel
{
    readonly IPackageRepository _packageRepository;

    public string? Filter { get; private set; }

    public string Sort { get; private set; } = null!;

    public IndexModel(IPackageRepository packageRepository)
    {
        _packageRepository = packageRepository;
    }

    public IPaginatedList<PackageItemViewModel> Packages { get; private set; } = null!;
    public int TotalPackageCount { get; private set; }
    public int CurrentUserOrganizationId { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? p, string? sort, string? filter)
    {
        int page = p ?? 1;
        string sortBy = sort ?? "Installs";
        await InitializePageAsync(page, WebConstants.LongPageSize, sortBy, filter);
        return Page();
    }

    async Task InitializePageAsync(int pageNumber, int pageSize, string sort, string? filter)
    {
        if (User.IsMember())
        {
            CurrentUserOrganizationId = HttpContext.RequireCurrentMember().OrganizationId;
        }

        Filter = filter;
        Sort = sort.ToLowerInvariant().Capitalize(); // Title case

        var packages = _packageRepository.GetQueryable();
        packages = packages.Where(p => p.Listed || p.OrganizationId == CurrentUserOrganizationId);
        TotalPackageCount = await packages.CountAsync();
        var filteredPackages = FilterPackages(packages, filter);

        filteredPackages = sort.ToUpperInvariant() switch
        {
            "NAME" => filteredPackages.OrderBy(s => s.Skill.Name),
            "INSTALLS" => filteredPackages.OrderByDescending(p => p.Versions.Sum(v => v.InstalledSkills.Count)),
            "UPDATED" => filteredPackages.OrderByDescending(s => s.Modified),
            "ORGANIZATION" => filteredPackages.OrderBy(s => s.Organization.Name),
            _ => filteredPackages.OrderBy(s => s.Skill.Name)
        };

        var packageList = filteredPackages;

        Packages = await PaginatedList.CreateAsync(
            packageList,
            pageNumber,
            pageSize,
            e => new PackageItemViewModel(e));
    }

    static IQueryable<Package> FilterPackages(IQueryable<Package> queryable,
        string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return queryable;
        }

        string filterLikeExpression = filter + "%";
        return queryable.Where(s => s.Skill.Name == filter
                                    || EF.Functions.ILike(s.Skill.Name, filterLikeExpression)
                                    || EF.Functions.ILike(s.Description,
                                        "%" + filterLikeExpression)
                                    || EF.Functions.ILike(s.Organization.Name!,
                                        filterLikeExpression)).AsQueryable();
    }
}
