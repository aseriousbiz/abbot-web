using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers;
using Serious.Abbot.Infrastructure.Security;

public class SkillRunnerApiControllerBaseTests : ControllerTestBase<SkillRunnerApiControllerBaseTests.TestController>
{
    public class TestController : SkillRunnerApiControllerBase
    {
        public async Task<IActionResult> GetAsync()
        {
            return Ok((Skill.Id, Member.User.Id));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Bogus")]
    public async Task ReturnsNotFoundIfUserNotAuthenticatedOrWrongAuthType(string? authenticationType)
    {
        AuthenticateAs(new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(Array.Empty<Claim>(), authenticationType),
        }));

        var (_, result) = await InvokeControllerAsync(c => c.GetAsync());

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(null, "skillId=42")]
    [InlineData("userId=42", null)]
    [InlineData("wibbity wobbity", "skillId=42")]
    [InlineData("userId=42", "hoop de doop")]
    public async Task ReturnsNotFoundIfUserIdOrSkillIdClaimMissingOrInvalid(string? nameIdentifier, string? aud)
    {
        var claims = new List<Claim>();
        if (nameIdentifier is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifier));
        }
        if (aud is not null)
        {
            claims.Add(new Claim("aud", aud));
        }

        AuthenticateAs(new ClaimsPrincipal(new[]
        {
            new ClaimsIdentity(claims, AuthenticationConfig.SkillTokenScheme),
        }));

        var (_, result) = await InvokeControllerAsync(c => c.GetAsync());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ExecutesActionAndSetsUserIdAndSkillIdIfValid()
    {
        var skill = await Env.CreateSkillAsync("test");
        AuthenticateAs(Env.TestData.Member, skill);

        var (_, result) = await InvokeControllerAsync(c => c.GetAsync());

        var objectResult = Assert.IsType<OkObjectResult>(result);
        var (skillId, userId) = Assert.IsType<(int, int)>(objectResult.Value);
        Assert.Equal(skill.Id, skillId);
        Assert.Equal(Env.TestData.User.Id, userId);
    }
}
