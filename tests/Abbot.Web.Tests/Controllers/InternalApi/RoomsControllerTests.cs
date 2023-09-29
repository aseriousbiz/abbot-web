using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Models.Api;
using Xunit;

public class RoomsControllerTests : ControllerTestBase<RoomsController>
{
    protected override string ExpectedArea => InternalApiControllerBase.Area;

    [Theory]
    [InlineData("", 100, new[] { "a-nice-room", "room-of-requirement", "ʏᴇʟʟɪɴɢ" })]
    [InlineData("", 2, new[] { "a-nice-room", "room-of-requirement" })]
    [InlineData("room", 100, new[] { "room-of-requirement", "a-nice-room" })]
    public async Task FindsRoomsInExpectedOrder(string query, int limit, string[] matchedNames)
    {
        var env = Env;

        // Create test rooms
        await env.CreateRoomAsync(name: "room-of-requirement");
        await env.CreateRoomAsync(name: "a-nice-room");
        await env.CreateRoomAsync(name: "ʏᴇʟʟɪɴɢ");

        // Run the query
        var (_, result) = await InvokeControllerAsync(async controller =>
            await controller.FindRoomAsync(query, limit));
        var okResult = Assert.IsType<OkObjectResult>(result);
        var model = Assert.IsAssignableFrom<IReadOnlyList<RoomResponseModel>>(okResult.Value);

        Assert.Equal(matchedNames, model.Select(r => r.Name).ToArray());
    }
}
