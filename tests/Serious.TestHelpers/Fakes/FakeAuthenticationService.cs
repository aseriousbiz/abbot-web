using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Serious.TestHelpers
{
    public class FakeAuthenticationService : IAuthenticationService
    {
        public AuthenticateResult ExpectedAuthenticationResult { get; set; }
            = AuthenticateResult.Fail("Unit test auth failed");

        public Task<AuthenticateResult> AuthenticateAsync(
            HttpContext context,
            string scheme)
        {
            return Task.FromResult(ExpectedAuthenticationResult);
        }

        public Task ChallengeAsync(
            HttpContext context,
            string scheme,
            AuthenticationProperties properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(
            HttpContext context,
            string scheme,
            AuthenticationProperties properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(
            HttpContext context,
            string scheme,
            ClaimsPrincipal principal,
            AuthenticationProperties properties)
        {
            SignInAsyncCalled = true;
            return Task.CompletedTask;
        }

        public Task SignOutAsync(
            HttpContext context,
            string scheme,
            AuthenticationProperties properties)
        {
            return Task.CompletedTask;
        }

        public bool SignInAsyncCalled { get; private set; }
    }
}
