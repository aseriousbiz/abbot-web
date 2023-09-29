using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Pages;

public static class ViewDataExtensions
{
    const string ViewDataPrefix = "__Abbot__";

    public static void SetViewer(this ViewDataDictionary viewData, Member viewer)
    {
        viewData[$"{ViewDataPrefix}:Viewer"] = viewer;
    }

    public static Member GetViewer(this ViewDataDictionary viewData)
    {
        if (!viewData.TryGetValue($"{ViewDataPrefix}:Viewer", out var v) || v is not Member viewer)
        {
            throw new UnreachableException("ViewDataExtensions.GetViewer() called without a Viewer set in ViewData!");
        }

        return viewer;
    }

    public static void SetOrganization(this ViewDataDictionary viewData, Organization organization)
    {
        viewData[$"{ViewDataPrefix}:Organization"] = organization;
    }

    public static Organization GetOrganization(this ViewDataDictionary viewData)
    {
        if (!viewData.TryGetValue($"{ViewDataPrefix}:Organization", out var v) || v is not Organization organization)
        {
            throw new UnreachableException("ViewDataExtensions.GetOrganization() called without an Organization set in ViewData!");
        }

        return organization;
    }
}
