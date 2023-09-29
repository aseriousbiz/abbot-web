using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    public class FakeControllerContext : ControllerContext
    {
        public FakeControllerContext() : this(new FakeHttpContext())
        {
        }

        public FakeControllerContext(ClaimsPrincipal principal) : this(new FakeHttpContext(principal))
        {
        }

        public FakeControllerContext(HttpContext httpContext)
            : base(new FakeActionContext(
                httpContext,
                new RouteData(),
                new ControllerActionDescriptor()))
        {
        }

        public FakeControllerContext(ActionContext actionContext)
            : base(actionContext)
        {
        }

        public FakeControllerContext(HttpContext httpContext, IRouter router)
            : base(new FakeActionContext(
                httpContext,
                new RouteData(),
                new ControllerActionDescriptor(), router))
        {
        }
    }
}
