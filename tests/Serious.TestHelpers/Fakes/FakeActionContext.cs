using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;

namespace Serious.TestHelpers
{
    public class FakeActionContext : ActionContext
    {
        public FakeActionContext()
            : this(new RouteData())
        {
        }

        public FakeActionContext(RouteData routeData)
            : this(new FakeHttpContext(), routeData, new ControllerActionDescriptor())
        {
        }

        public FakeActionContext(HttpContext httpContext, ActionDescriptor actionDescriptor)
            : this(httpContext, new RouteData(), actionDescriptor)
        {
        }

        public FakeActionContext(HttpContext httpContext, RouteData routeData, ActionDescriptor actionDescriptor)
            : this(httpContext, routeData, actionDescriptor, new FakeRouter())
        {
        }

        public FakeActionContext(HttpContext httpContext, RouteData routeData, ActionDescriptor actionDescriptor, IRouter router)
            : base(httpContext, routeData, actionDescriptor)
        {
            if (routeData.Routers.Count == 0)
            {
                routeData.Routers.Add(router);
            }
        }

        public async Task<string?> GetResponseBodyAsync()
        {
            return HttpContext is FakeHttpContext fakeHttpContext
                ? await fakeHttpContext.GetResponseBodyAsync()
                : null;
        }
    }
}
