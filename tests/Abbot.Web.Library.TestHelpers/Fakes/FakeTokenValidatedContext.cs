using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Serious.TestHelpers
{
    public class FakeTokenValidatedContext : TokenValidatedContext
    {
        public FakeTokenValidatedContext(ClaimsPrincipal principal)
            : this(
                new FakeHttpContext(principal),
                new AuthenticationScheme("openId", "Open ID", Substitute.For<IAuthenticationHandler>().GetType()),
                new OpenIdConnectOptions(),
                principal,
                new AuthenticationProperties())
        {
        }

        public FakeTokenValidatedContext(
            HttpContext context,
            AuthenticationScheme scheme,
            OpenIdConnectOptions options,
            ClaimsPrincipal principal,
            AuthenticationProperties properties) : base(context, scheme, options, principal, properties)
        {
        }
    }
}
