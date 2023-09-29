using System;
using Serious.AspNetCore;

namespace Serious.Abbot.Components;

public class BreadCrumbItem
{
    readonly string _href;
    readonly string _currentPath;

    public BreadCrumbItem(string href, string currentPath)
        : this(href, currentPath, href.GetPageName().Capitalize())
    {
    }

    public BreadCrumbItem(string href, string currentPath, string pageName)
    {
        _href = href.TrimSuffix("/", StringComparison.Ordinal);
        _currentPath = currentPath.TrimSuffix("/", StringComparison.Ordinal);
        Name = pageName;
        Active = IsSamePage(href, currentPath);
        Href = Active ? "#" : href;
        IconName = null;
    }

    public string Name { get; }
    public bool Active { get; }
    public string? IconName { get; }
    public string Href { get; }

    public BreadCrumbItem? Parent
    {
        get {
            var parentHref = _href
                .LeftBefore("/", StringComparison.OrdinalIgnoreCase);

            if (parentHref.Length == 0)
            {
                return null;
            }
            return new BreadCrumbItem(parentHref, _currentPath);
        }
    }

    static bool IsSamePage(string? page, string? path)
    {
        if (page is null || path is null)
        {
            return false;
        }

        page = page.TrimLeadingCharacter('/').EnsureTrailingCharacter('/');
        path = path.TrimLeadingCharacter('/').EnsureTrailingCharacter('/');

        return path.Equals(page, StringComparison.OrdinalIgnoreCase);
    }
}
