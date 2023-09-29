using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    public class FakePageHandlerExecutedContext : PageHandlerExecutedContext
    {
        public FakePageHandlerExecutedContext(HttpContext httpContext)
            : this(httpContext, new ActionDescriptor())
        {
        }

        FakePageHandlerExecutedContext(HttpContext httpContext, ActionDescriptor actionDescriptor)
            : this(
                httpContext,
                actionDescriptor, new RouteData())
        {
        }

        FakePageHandlerExecutedContext(
            HttpContext httpContext,
            ActionDescriptor actionDescriptor,
            RouteData routeData)
            : this(
                new FakePageContext(httpContext, routeData, actionDescriptor),
                new List<IFilterMetadata>(),
                new HandlerMethodDescriptor(),
                new FakePageModel())
        {
        }

        public FakePageHandlerExecutedContext(HttpContext httpContext, bool allowAnonymous, string currentPage)
            : this(httpContext, FilterHelpers.GetActionDescriptor(allowAnonymous), FilterHelpers.GetRouteDataForPage(currentPage))
        {
        }

        FakePageHandlerExecutedContext(
            PageContext pageContext,
            IList<IFilterMetadata> filters,
            HandlerMethodDescriptor handlerMethod,
            object handlerInstance)
            : base(pageContext, filters, handlerMethod, handlerInstance)
        {
        }
    }
}
