using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Security;
using Xunit;

public class ApiKeyAuthenticationHandlerTests
{
    public class TheHandleChallengeAsyncMethod
    {
        [Fact]
        public async Task ReportsAJsonProblemWith401StatusCode()
        {
            var env = TestEnvironment.Create();
            var context = new DefaultHttpContext();
            var body = new MemoryStream();
            context.Response.Body = body;

            var handler = env.Activate<ApiKeyAuthenticationHandler>();
            await handler.InitializeAsync(
                new AuthenticationScheme("Test", "Test", typeof(ApiKeyAuthenticationHandler)),
                context);
            await handler.ChallengeAsync(new AuthenticationProperties());

            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);

            body.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(body);
            var actual = await reader.ReadToEndAsync();
            Assert.Equal("{\"type\":\"https://schemas.ab.bot/problems/auth_failed\",\"title\":\"Authentication Failed\",\"status\":401,\"detail\":\"Authentication failed. Either the 'Authorization' headed was missing, invalid, or didn't include a valid API Key. Go to your Account Settings to generate a valid API Key.\",\"instance\":\"https://schemas.ab.bot/problems/auth_failed\"}", actual);
        }
    }

    public class TheHandleAuthenticateAsyncMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Poopy")]
        [InlineData("Bearer ")]
        public async Task ReturnsNoResultIfAuthHeaderInvalid(string? authHeaderValue)
        {
            var env = TestEnvironment.Create();
            var context = new DefaultHttpContext();
            if (authHeaderValue is not null)
            {
                context.Request.Headers["Authorization"] = authHeaderValue;
            }

            var handler = env.Activate<ApiKeyAuthenticationHandler>();
            await handler.InitializeAsync(
                new AuthenticationScheme("Test", "Test", typeof(ApiKeyAuthenticationHandler)),
                context);

            var result = await handler.AuthenticateAsync();

            Assert.True(result.None);
        }

        [Fact]
        public async Task ReturnsFailIfNoMemberWithApiKey()
        {
            var env = TestEnvironment.Create();
            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = "Bearer abk_123";

            var handler = env.Activate<ApiKeyAuthenticationHandler>();
            await handler.InitializeAsync(
                new AuthenticationScheme("Test", "Test", typeof(ApiKeyAuthenticationHandler)),
                context);

            var result = await handler.AuthenticateAsync();

            Assert.Equal("Invalid token", result.Failure?.Message);
        }

        [Fact]
        public async Task ReturnsSuccessWithPrincipalAndCurrentMemberIfKeyMatchesMember()
        {
            var env = TestEnvironment.Create();
            var admin = await env.CreateAdminMemberAsync();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, admin);
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Administrator, admin);

            var key = await env.Users.CreateApiKeyAsync("test", 365, env.TestData.Member);

            var context = new DefaultHttpContext();
            context.Request.Headers["Authorization"] = $"Bearer {key.Token}";

            var handler = env.Activate<ApiKeyAuthenticationHandler>();
            await handler.InitializeAsync(
                new AuthenticationScheme("Test", "Test", typeof(ApiKeyAuthenticationHandler)),
                context);

            var result = await handler.AuthenticateAsync();

            Assert.Equal(env.TestData.Member.Id, context.GetCurrentMember()?.Id);
            Assert.True(result.Succeeded);
            Assert.Equal("Test", result.Principal?.Identity?.AuthenticationType);
            Assert.Equal(new[]
            {
                $"https://schemas.ab.bot/abbot_member_id: {env.TestData.Member.Id}",
                $"{ClaimTypes.Role}: Agent",
                $"{ClaimTypes.Role}: Administrator",
            }, result.Principal?.Claims.Select(c => c.ToString()).ToArray());
        }
    }
}
