using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Serious.Abbot.Security;

public class StaffViewablePageConvention : IPageRouteModelConvention
{
    readonly HashSet<string> _paths;

    public StaffViewablePageConvention(IEnumerable<string> paths)
    {
        _paths = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
    }

    public void Apply(PageRouteModel model)
    {
        if (_paths.Contains(model.ViewEnginePath) && model.Selectors.Count > 0)
        {
            var newSelectors = new List<SelectorModel>();

            var orderedSelectors = model.Selectors.OrderBy(s => s.AttributeRouteModel?.Order ?? 0).ToList();
            var minOrder = orderedSelectors.First().AttributeRouteModel?.Order ?? 0;
            var staffBaseOrder = minOrder - model.Selectors.Count;

            for (var index = 0; index < model.Selectors.Count; index++)
            {
                var newSelector = new SelectorModel(model.Selectors[index]);
                if (newSelector.AttributeRouteModel is null)
                {
                    continue;
                }

                var template = newSelector.AttributeRouteModel.Template is ['/', ..]
                    ? newSelector.AttributeRouteModel.Template[1..]
                    : newSelector.AttributeRouteModel.Template;

                newSelector.AttributeRouteModel.Template = "/staff/organizations/{staffOrganizationId}/"
                                                           + template;

                // Set the order of this route so that it runs BEFORE the original route.
                // That way, if there is a 'staffOrganizationId' in the route values, it will be used.
                newSelector.AttributeRouteModel.Order = staffBaseOrder + index;

                newSelectors.Add(newSelector);
            }

            model.Selectors.AddRange(newSelectors);
        }
    }
}

public static class StaffViewablePageConventionExtensions
{
    /// <summary>
    /// Marks all the pages with the specified names as staff-viewable.
    /// </summary>
    /// <remarks>
    /// A Staff-Viewable page has an additional route that is the same as the original route, but with
    /// the prefix '/staff/organizations/{staffOrganizationId}/' prepended.
    /// When viewed from this route, the viewer must be staff, but the page will be rendered in the context of the original organization.
    /// </remarks>
    public static void AddStaffViewablePages(this PageConventionCollection conventions, params string[] paths)
    {
        conventions.Add(new StaffViewablePageConvention(paths));
    }
}
