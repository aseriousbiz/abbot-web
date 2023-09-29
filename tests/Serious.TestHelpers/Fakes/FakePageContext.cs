using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    public class FakePageContext : PageContext
    {
        public FakePageContext() : this(new FakeHttpContext())
        {
        }

        public FakePageContext(ClaimsPrincipal principal) : this(new FakeHttpContext
        {
            User = principal
        })
        {
        }

        public FakePageContext(ActionContext actionContext) : base(actionContext)
        {
        }

        public FakePageContext(
            HttpContext httpContext,
            RouteData routeData,
            ActionDescriptor actionDescriptor)
            : this(new FakeActionContext(httpContext, routeData, actionDescriptor))
        {
        }

        FakePageContext(HttpContext httpContext)
            : this(httpContext, new RouteData(), new CompiledPageActionDescriptor
            {
                EndpointMetadata = new List<object>()
            })
        {
        }
    }
}
