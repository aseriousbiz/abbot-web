using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Pages;
using Serious.AspNetCore;

namespace Serious.Abbot.Components;

[ViewComponent]
public class BreadCrumb : ViewComponent
{
    public IViewComponentResult Invoke(string title)
    {
        var currentPath = ViewContext.GetPath();

        var currentItem = new BreadCrumbItem(currentPath, currentPath, title);
        var items = GetBreadCrumbs(currentItem).Reverse().ToList();
        if (items.Count == 1)
        {
            // We don't want to show the breadcrumb when we're at the root. It just looks weird.
            items.RemoveAt(0);
        }

        return View(items);
    }

    static IEnumerable<BreadCrumbItem> GetBreadCrumbs(BreadCrumbItem? currentItem)
    {
        while (currentItem is not null)
        {
            yield return currentItem;
            currentItem = currentItem.Parent;
        }
    }
}
