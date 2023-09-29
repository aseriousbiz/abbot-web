using System.Security.Claims;
using Serious.Abbot.Security;
using Xunit;

public class ClaimPrincipalExtensionsTests
{
    public class TheGetPlatformUserIdMethod
    {
        [Fact]
        public void GetsPlatformUserIdFromPlatformUserIdClaim()
        {
            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim($"{AbbotSchema.SchemaUri}platform_user_id", "PlatformUserId"));
            var principal = new ClaimsPrincipal(identity);

            var result = principal.GetPlatformUserId();

            Assert.Equal("PlatformUserId", result);
        }
    }
}
