using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    public class FakePageHandlerExecutingContext : PageHandlerExecutingContext
    {
        public FakePageHandlerExecutingContext(
            HttpContext httpContext,
            bool allowAnonymous,
            string currentPage = "/Index")
            : this(httpContext, FilterHelpers.GetActionDescriptor(allowAnonymous), FilterHelpers.GetRouteDataForPage(currentPage))
        {
        }

        FakePageHandlerExecutingContext(
            HttpContext httpContext,
            ActionDescriptor actionDescriptor,
            RouteData routeData)
            : this(
                new FakePageContext(httpContext, routeData, actionDescriptor),
                new List<IFilterMetadata>(),
                new HandlerMethodDescriptor(),
                new Dictionary<string, object>(),
                new FakePageModel())
        {
        }

        FakePageHandlerExecutingContext(
            PageContext pageContext,
            IList<IFilterMetadata> filters,
            HandlerMethodDescriptor handlerMethod,
            IDictionary<string, object> handlerArguments,
            object handlerInstance)
            : base(pageContext, filters, handlerMethod, handlerArguments, handlerInstance)
        {
        }
    }
}
