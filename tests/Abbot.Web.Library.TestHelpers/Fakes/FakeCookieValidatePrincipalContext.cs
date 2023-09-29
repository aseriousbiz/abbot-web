using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Serious.TestHelpers
{
    public class FakeCookieValidatePrincipalContext : CookieValidatePrincipalContext
    {
        private const string DefaultScheme = "FakeCookie";

        public FakeCookieValidatePrincipalContext(AuthenticationProperties? properties = null)
            : this(new FakeClaimsPrincipal())
        {
        }

        public FakeCookieValidatePrincipalContext(ClaimsPrincipal principal, AuthenticationProperties? properties = null)
            : base(
                 new FakeHttpContext(principal),
                 new AuthenticationScheme(DefaultScheme, "Fake Cookie", typeof(CookieAuthenticationHandler)),
                 new CookieAuthenticationOptions(),
                 new AuthenticationTicket(principal, properties, DefaultScheme)
             )
        {
        }
    }
}
