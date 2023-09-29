using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters.Json.Internal;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Serious.TestHelpers
{
    public class FakeHttpContext : HttpContext
    {
        readonly HttpContext _httpContext;

        public async Task<string?> GetResponseBodyAsync()
        {
            if (_httpContext.Response.Body is MemoryStream stream)
            {
                stream.Position = 0;
                var newStream = new MemoryStream();
                await stream.CopyToAsync(newStream, RequestAborted);
                newStream.Position = 0;
                return await new StreamReader(newStream).ReadToEndAsync();
            }

            return null;
        }

        public FakeHttpContext()
            : this(new ClaimsPrincipal(new[] { new ClaimsIdentity() }))
        {
        }

        public FakeHttpContext(ClaimsPrincipal principal)
            : this(CreateDefaultHttpContext(principal))
        {
        }

        static HttpContext CreateDefaultHttpContext(ClaimsPrincipal principal)
        {
            var httpContext = new DefaultHttpContext
            {
                User = principal
            };

            var response = httpContext.Response;
            response.Body = new MemoryStream();
            var request = httpContext.Request;
            request.Protocol = "https";
            request.Host = new HostString("localhost");
            request.Body = new MemoryStream();
            return httpContext;
        }

        FakeHttpContext(HttpContext httpContext)
        {
            _httpContext = httpContext;

            Features = _httpContext.Features;
            Request = _httpContext.Request;
            Response = _httpContext.Response;
            Connection = _httpContext.Connection;
            WebSockets = _httpContext.WebSockets;
            var requestServices = new FakeServiceProvider(httpContext.RequestServices);
            requestServices.AddService<IActionResultExecutor<ContentResult>>(new FakeContentResultExecutor());
            requestServices.AddService<IActionResultExecutor<JsonResult>>(new FakeJsonResultExecutor());
            requestServices.AddService<IAuthenticationService>(new FakeAuthenticationService());
            requestServices.AddService<IUrlHelperFactory>(new FakeUrlHelperFactory());
            _httpContext.RequestServices = requestServices;
        }

        public override void Abort()
        {
            _httpContext.Abort();
        }

        public override IFeatureCollection Features { get; }
        public override HttpRequest Request { get; }
        public override HttpResponse Response { get; }
        public override ConnectionInfo Connection { get; }
        public override WebSocketManager WebSockets { get; }

#pragma warning disable 618
#pragma warning disable 672
        public override AuthenticationManager Authentication => _httpContext.Authentication;
#pragma warning restore 672
#pragma warning restore 618

        public override ClaimsPrincipal User
        {
            get => _httpContext.User;
            set => _httpContext.User = value;
        }

        public override IDictionary<object, object> Items
        {
            get => _httpContext.Items;
            set => _httpContext.Items = value;
        }

        public override IServiceProvider RequestServices
        {
            get => _httpContext.RequestServices;
            set => _httpContext.RequestServices = value;
        }

        public override CancellationToken RequestAborted
        {
            get => _httpContext.RequestAborted;
            set => _httpContext.RequestAborted = value;
        }

        public override string TraceIdentifier
        {
            get => _httpContext.TraceIdentifier;
            set => _httpContext.TraceIdentifier = value;
        }

        public override ISession Session
        {
            get => _httpContext.Session;
            set => _httpContext.Session = value;
        }
    }
}
