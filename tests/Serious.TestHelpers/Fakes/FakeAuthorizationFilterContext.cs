using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    public class FakeAuthorizationFilterContext : AuthorizationFilterContext
    {
        public FakeAuthorizationFilterContext(
            HttpContext httpContext,
            ActionDescriptor actionDescriptor)
            : this(
                new FakeActionContext(httpContext, actionDescriptor),
                new List<IFilterMetadata>())
        {
        }

        public FakeAuthorizationFilterContext(HttpContext httpContext,
            RouteData routeData,
            ActionDescriptor actionDescriptor,
            IList<IFilterMetadata> filters)
            : this(new FakeActionContext(httpContext, routeData, actionDescriptor), filters)
        {
        }

        public FakeAuthorizationFilterContext(ActionContext actionContext, IList<IFilterMetadata> filters)
            : base(actionContext, filters)
        {
        }
    }
}
