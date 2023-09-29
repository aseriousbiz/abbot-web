using Microsoft.AspNetCore.Mvc;
using NodaTime;
using Serious.Abbot.Api;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.TestHelpers;

public class InsightsControllerTests : ControllerTestBase<InsightsController>
{
    protected override string ExpectedArea => InternalApiControllerBase.Area;

    public class TheGetVolumeAsyncMethod : InsightsControllerTests
    {
        [Fact]
        public async Task ReturnsVolumeForPassedInValuesAndSomeDefaults()
        {
            var env = Env;
            // Need at least one conversation in the database to get a volume.
            var room = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(room, env.TestData.Member, RoomRole.FirstResponder, env.TestData.Member);
            await env.CreateConversationAsync(room);
            env.InsightsRepository.SetConversationVolumeRollups(new[]
            {
                new ConversationVolumePeriod(new LocalDate(2020, 1, 1), 23, 24, 25),
            });

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetVolumeAsync(tz: "America/New_York"));

            var model = Assert.IsType<ConversationVolumeResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal("America/New_York", model.TimeZone);
            Assert.NotNull(env.InsightsRepository.Organization);
            Assert.Equal(env.TestData.Organization.Id, env.InsightsRepository.Organization.Id);
            Assert.NotNull(env.InsightsRepository.DatePeriodSelector);
            Assert.Equal(7, env.InsightsRepository.DatePeriodSelector.Days);
            Assert.IsType<ResponderRoomSelector>(env.InsightsRepository.RoomSelector);
        }

        [Fact]
        public async Task ReturnsVolumeForYearToDateWhenYearOfDataNotExist()
        {
            // Let's use a real InsightsRepository for this test.
            Builder.ReplaceService<IInsightsRepository, InsightsRepository>();

            var env = Env;
            await env.AddUserToRoleAsync(env.TestData.Member, Roles.Administrator);
            env.Clock.TravelTo(Dates.ParseUtc("May 11, 2022 22:53:00"));
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            await env.Rooms.AssignMemberAsync(room, env.TestData.Member, RoomRole.FirstResponder, env.TestData.Member);
            await env.CreateConversationAsync(room, timestamp: Dates.ParseUtc("March 23, 2022 19:58:00"));

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetVolumeAsync(
                    tz: "America/Los_Angeles",
                    range: DateRangeOption.Year,
                    filter: room.PlatformRoomId
                ));

            var model = Assert.IsType<ConversationVolumeResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal("America/Los_Angeles", model.TimeZone);
            // Since we don't have data for the year, we should get the data for the year to date.
            Assert.Equal(50, model.Data.Count);
            // The first day should be the day the first conversation was created.
            var firstDay = model.Data[0];
            Assert.Equal(1, firstDay.New);
            Assert.Equal(new LocalDate(2022, 03, 23), firstDay.Date);
        }

        [Fact]
        public async Task UsesAllRoomsWhenAllPassed()
        {
            var env = Env;
            await env.AddUserToRoleAsync(env.TestData.Member, Roles.Administrator);
            // Need at least one conversation in the database to get a volume.
            await env.CreateConversationAsync(await env.CreateRoomAsync());
            env.InsightsRepository.SetConversationVolumeRollups(new[]
            {
                new ConversationVolumePeriod(new LocalDate(2020, 1, 1), 23, 24, 25),
            });

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetVolumeAsync(filter: InsightsRoomFilter.All));

            Assert.IsType<ConversationVolumeResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.Equal(RoomSelector.AllRooms, env.InsightsRepository.RoomSelector);
        }

        [Fact]
        public async Task ShowsVolumeForSpecificRoom()
        {
            var env = Env;
            await env.AddUserToRoleAsync(env.TestData.Member, Roles.Administrator);
            var room0 = await env.CreateRoomAsync();

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetVolumeAsync(filter: room0.PlatformRoomId));

            Assert.IsType<ConversationVolumeResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
            var roomSelector = Assert.IsType<SpecificRoomsSelector>(env.InsightsRepository.RoomSelector);
            var roomId = Assert.Single(roomSelector.RoomIds);
            Assert.Equal(room0.Id, roomId);
        }

        [Fact]
        public async Task ShowsVolumeForAssignedRooms()
        {
            var env = Env;

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetVolumeAsync(filter: InsightsRoomFilter.Yours));

            Assert.IsType<ConversationVolumeResponseModel>(Assert.IsType<OkObjectResult>(result).Value);
            Assert.IsType<ResponderRoomSelector>(env.InsightsRepository.RoomSelector);
        }
    }

    public class TheGetConversationVolumeByRoomAsyncMethod : InsightsControllerTests
    {
        [Fact]
        public async Task GetsConversationVolumePerRoom()
        {
            var env = Env;
            await env.AddUserToRoleAsync(env.TestData.Member, Roles.Administrator);
            env.InsightsRepository.SetConversationVolumeByRoom(
                new RoomConversationVolume(
                    CreateRoomInstance("room1", ("U875309", "https://example.com/a1.jpg")),
                    23),
                new RoomConversationVolume(
                    CreateRoomInstance("room2", Array.Empty<(string, string)>()),
                    21),
                new RoomConversationVolume(
                    CreateRoomInstance("room3", ("U999999", "https://example.com/a2.jpg"), ("U8675309", "https://example.com/a1.jpg")),
                    16));

            var (_, result) = await InvokeControllerAsync(async controller =>
                await controller.GetConversationVolumeByRoomAsync(DateRangeOption.Month, filter: InsightsRoomFilter.All));

            var partial = Assert.IsType<PartialViewResult>(result);
            var model = Assert.IsType<InsightRoomConversationVolumeViewModel>(partial.Model);
            Assert.Equal(60, model.TotalOpenConversationCount);
        }

        static Room CreateRoomInstance(string name, params (string, string)[] firstResponders)
        {
            return new Room
            {
                Name = name,
                Assignments = firstResponders.Select(r => new RoomAssignment
                {
                    Member = new() { User = new() { PlatformUserId = r.Item1, Avatar = r.Item2 } },
                }).ToList()
            };
        }
    }
}
