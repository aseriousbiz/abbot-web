using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.AspNetCore;

namespace Serious.Abbot.Pages;

public static class ViewContextExtensions
{
    public static string GetRawUrl(this ViewContext viewContext)
    {
        return viewContext.HttpContext.GetRawUrl();
    }

    public static string GetPageName(this ViewContext viewContext)
    {
        return viewContext.HttpContext.GetPageName();
    }

    public static string GetPath(this ViewContext viewContext)
    {
        return viewContext.HttpContext.GetPath();
    }

    public static PageInfo GetPageInfo(this ViewContext viewContext)
    {
        return viewContext.ViewBag.PageInfo ??
#if DEBUG
            throw new InvalidOperationException("PageInfo not set in ViewBag");
#else
            viewContext.HttpContext.GetPageInfo();
#endif
    }

    public static void SetPageInfo(this ViewContext viewContext, PageInfo pageInfo)
    {
        if (viewContext.ViewData.Model is StaffViewablePage { InStaffTools: true } staffPage)
        {
            pageInfo = pageInfo with
            {
                Title = $"Staff - {pageInfo.Title}",
                Category = $"Staff/{pageInfo.Category}"
            };
        }
        viewContext.ViewBag.PageInfo = pageInfo;
    }

    public static void SetPageInfo(this ViewContext viewContext, string category, string name, string title)
    {
        viewContext.SetPageInfo(new PageInfo(category, name, title));
    }

    public static void SetPageInfo(this ViewContext viewContext, string category, string name)
    {
        viewContext.SetPageInfo(new PageInfo(category, name));
    }

    /// <summary>
    /// This returns a dictionary of all current route values combined with query string values.
    /// This makes it easy to generate links to the current page with different query string values
    /// by using this in asp-all-route-data. Just make sure to set asp-route-* after asp-all-route-data.
    /// </summary>
    /// <remarks>
    /// Note that this doesn't work for cases where there's more than one value for a query string key.
    /// </remarks>
    /// <param name="actionContext">The current action context.</param>
    public static IDictionary<string, string> GetCurrentRouteValues(this ActionContext actionContext)
    {
        var routeValues = actionContext
            .RouteData
            .Values
            .ToDictionary(x => x.Key, x => $"{x.Value}");
        // Add any query string parameters to the list
        foreach (var (key, value) in actionContext.HttpContext.Request.Query)
        {
            routeValues[key] = $"{value}";
        }

        return routeValues;
    }
}
