using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Skills;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Packages;

public class Sitemap : CustomSkillPageModel
{
    readonly IPackageRepository _packageRepository;

    public IList<SitemapViewModel> SitemapItems { get; }

    public Sitemap(IPackageRepository packageRepository)
    {
        _packageRepository = packageRepository;
        SitemapItems = new List<SitemapViewModel>();
    }

    public IActionResult OnGet()
    {
        var packages = _packageRepository.GetQueryable();
        packages = packages.Where(p => p.Listed);

        string baseUrl = $"https://{WebConstants.DefaultHost}/packages/";

        foreach (var package in packages)
        {
            var packageUrl = $"{package.Skill.Name}-for-Slack-by-{package.Organization.Slug}";
            var pageUrl = baseUrl + packageUrl;
            var sitemapItem = new SitemapViewModel { Loc = pageUrl, LastMod = package.Modified };
            SitemapItems.Add(sitemapItem);
        }

        return Page();
    }
}
