using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Serious.Abbot.Infrastructure;

public static class RazorConventions
{
    /// <summary>
    /// Removes all the routes from the specified page and replaces them with the specified list of routes.
    /// </summary>
    /// <remarks>
    /// When adding routes to a page, make sure the more specific ones
    /// (the ones that consume more route values) come first.
    /// Otherwise, they could end up being matched by the more general one
    /// and the remaining route values will end up as query string parameters.
    /// </remarks>
    public static void AddMultiRoutePage(this PageConventionCollection conventions, string pagePath, params string[] routes)
    {
        conventions.AddPageRouteModelConvention(pagePath,
            model => {
                model.Selectors.Clear();

                // Make sure theses routes are in the order they were specified in
                for (var index = 0; index < routes.Length; index++)
                {
                    model.Selectors.Add(new()
                    {
                        AttributeRouteModel = new()
                        {
                            Order = index,
                            Template = routes[index],
                        }
                    });
                }
            });
    }
}
