using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    internal static class FilterHelpers
    {
        public static ActionDescriptor GetActionDescriptor(bool allowAnonymous)
        {
            var descriptor = new CompiledPageActionDescriptor
            {
                EndpointMetadata = new List<object>()
            };
            if (allowAnonymous)
            {
                descriptor.EndpointMetadata.Add(new AllowAnonymousAttribute());
            }

            return descriptor;
        }

        public static RouteData GetRouteDataForPage(string page)
        {
            return new RouteData(new RouteValueDictionary
            {
                {"page", page}
            });
        }
    }
}
