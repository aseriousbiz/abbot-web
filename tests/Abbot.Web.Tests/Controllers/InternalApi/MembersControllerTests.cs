using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Models.Api;
using Xunit;

public class MembersControllerTests : ControllerTestBase<MembersController>
{
    protected override string? ExpectedArea => InternalApiControllerBase.Area;

    [Theory]
    [InlineData("", 100, new[] { "George", "John", "Paul", "Ringo" })]
    [InlineData("r", 100, new[] { "Ringo", "George", "Paul" })] // Match at the front of the name sorts first (and Paul has an 'r' in his last name)
    [InlineData("o", 100, new[] { "George", "John", "Ringo" })]
    [InlineData("starr", 100, new[] { "Ringo" })] // Searches real name and display name
    [InlineData("o", 2, new[] { "George", "John" })]
    public async Task FindsMembersInExpectedOrder(string query, int limit, string[] matchedDisplayNames)
    {
        var env = Env;
        env.Db.Members.Remove(env.TestData.Member);
        await env.Db.SaveChangesAsync();

        // Create test members
        var john = await env.CreateMemberAsync(realName: "John Lennon", displayName: "John");
        await env.CreateMemberAsync(realName: "Paul McCartney", displayName: "Paul");
        await env.CreateMemberAsync(realName: "Ringo Starr", displayName: "Ringo");
        await env.CreateMemberAsync(realName: "George Harrison", displayName: "George");

        // Run the query
        var (_, result) = await InvokeControllerAsync(async controller =>
            await controller.FindUserAsync(query, limit));
        var okResult = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<MemberResponseModel>>(okResult.Value);

        Assert.Equal(matchedDisplayNames, model.Select(m => m.NickName).ToArray());
    }
}
