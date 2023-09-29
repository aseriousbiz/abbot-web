using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Pages.Settings.Account;
using Serious.Abbot.Security;
using Serious.Filters;

public static class AccountIndexPageTests
{
    public class TheOnPostAsyncMethod : PageTestBase<IndexPage>
    {
        [Theory]
        [InlineData("09:45", "10:00", "Working hours must be specified on the hour or half-hour.")]
        [InlineData("09:00", "10:27", "Working hours must be specified on the hour or half-hour.")]
        [InlineData(null, "10:00", "Both start and end working hours must be specified.")]
        [InlineData("09:00", null, "Both start and end working hours must be specified.")]
        [InlineData("poop", "09:00", "Working hours must be specified as 'HH:mm', in 24-hour time.")]
        [InlineData("09:00", "borpo", "Working hours must be specified as 'HH:mm', in 24-hour time.")]
        public async Task RedirectsWithMessageIfWorkingHoursAreNotValid(string? startTime,
            string? endTime, string message)
        {
            var (page, result) = await InvokePageAsync(p => {
                p.WorkingHoursStart = startTime;
                p.WorkingHoursEnd = endTime;
                return p.OnPostAsync(null, 1);
            });

            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal(message, page.StatusMessage);
        }

        [Fact]
        public async Task UpdatesWorkingHoursIfTheyAreValid()
        {
            Assert.Null(TestMember.WorkingHours);

            var (page, result) = await InvokePageAsync(p => {
                p.WorkingHoursStart = "10:30";
                p.WorkingHoursEnd = "20:00";
                return p.OnPostAsync(null, 1);
            });

            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("Working hours updated.", page.StatusMessage);

            await Env.ReloadAsync(TestMember);
            Assert.Equal(new TimeOnly(10, 30), TestMember.WorkingHours?.Start);
            Assert.Equal(new TimeOnly(20, 0), TestMember.WorkingHours?.End);
        }
    }

    public class TheOnGetAsyncMethod : PageTestBase<IndexPage>
    {
        [Fact]
        public async Task UsesDefaultWorkingHoursIfCurrentUserHasNotSetWorkingHours()
        {
            Assert.Null(TestMember.WorkingHours);

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync(p: 1));

            Assert.Equal("09:00", page.WorkingHoursStart);
            Assert.Equal("17:00", page.WorkingHoursEnd);
        }

        [Fact]
        public async Task LoadsUsersWorkingHours()
        {
            TestMember.WorkingHours = new WorkingHours(new(10, 0), new(16, 0));
            await Env.Db.SaveChangesAsync();

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync(p: 1));

            Assert.Equal("10:00", page.WorkingHoursStart);
            Assert.Equal("16:00", page.WorkingHoursEnd);
        }

        [Fact]
        public async Task LoadsConversationRoomsWithAssignmentsForTheCurrentUser()
        {
            await Env.Roles.AddUserToRoleAsync(TestMember, Roles.Agent, Env.TestData.Abbot);
            var room1 = await Env.CreateRoomAsync(name: "Room A", managedConversationsEnabled: true);
            await Env.Rooms.AssignMemberAsync(room1, TestMember, RoomRole.FirstResponder, TestMember);
            await Env.Rooms.AssignMemberAsync(room1, TestMember, RoomRole.EscalationResponder, TestMember);
            var room2 = await Env.CreateRoomAsync(name: "Room B", managedConversationsEnabled: false);
            await Env.Rooms.AssignMemberAsync(room2, TestMember, RoomRole.EscalationResponder, TestMember);
            var room3 = await Env.CreateRoomAsync(name: "Room C", managedConversationsEnabled: true);
            await Env.Rooms.AssignMemberAsync(room3, TestMember, RoomRole.EscalationResponder, TestMember);

            var (page, result) = await InvokePageAsync(p => p.OnGetAsync(p: 1));

            Assert.IsType<PageResult>(result);
            Assert.Collection(page.Rooms,
                r1 => {
                    Assert.Equal(r1.Room.Id, room1.Id);
                    Assert.Equal(r1.Room.Name, room1.Name);
                    Assert.True(r1.IsFirstResponder);
                    Assert.True(r1.IsEscalationResponder);
                },
                r2 => {
                    Assert.Equal(r2.Room.Id, room3.Id);
                    Assert.Equal(r2.Room.Name, room3.Name);
                    Assert.False(r2.IsFirstResponder);
                    Assert.True(r2.IsEscalationResponder);
                });
        }

        [Fact]
        public async Task CanFilterByName()
        {
            await Env.CreateRoomAsync(name: "the-danger-room", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "the-cool-room", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "the-boring-room", managedConversationsEnabled: true);

            // Load the rooms
            var (page, _) = await InvokePageAsync(p => {
                p.Filter = new FilterList { new("cool") };
                return p.OnGetAsync();
            });

            // Check the result
            Assert.Equal("cool", page.Filter.ToString());
            Assert.Equal(
                new[] { "the-cool-room" },
                page.Rooms.Select(r => r.Room.Name).ToArray());
        }

        [Fact]
        public async Task CanPaginate()
        {
            for (int i = 0; i < 21; i++)
            {
                await Env.CreateRoomAsync(name: $"r-{i:00}", managedConversationsEnabled: true);
            }

            // Load the rooms
            var (page1, _) = await InvokePageAsync(p => p.OnGetAsync(p: 1));
            var (page2, _) = await InvokePageAsync(p => p.OnGetAsync(p: 2));
            var (page3, _) = await InvokePageAsync(p => p.OnGetAsync(p: 3));
            var (page4, _) = await InvokePageAsync(p => p.OnGetAsync(p: 4));

            // Check the result
            Assert.Equal(new[] { "r-00", "r-01", "r-02", "r-03", "r-04", "r-05", "r-06", "r-07", "r-08", "r-09" },
                page1.Rooms.Select(r => r.Room.Name).ToArray());

            Assert.False(page1.Rooms.HasPreviousPage);
            Assert.True(page1.Rooms.HasNextPage);
            Assert.Equal(new[] { "r-10", "r-11", "r-12", "r-13", "r-14", "r-15", "r-16", "r-17", "r-18", "r-19" },
                page2.Rooms.Select(r => r.Room.Name).ToArray());

            Assert.True(page2.Rooms.HasPreviousPage);
            Assert.True(page2.Rooms.HasNextPage);
            Assert.Equal(new[] { "r-20" },
                page3.Rooms.Select(r => r.Room.Name).ToArray());

            Assert.True(page3.Rooms.HasPreviousPage);
            Assert.False(page3.Rooms.HasNextPage);
            Assert.Empty(page4.Rooms);
            Assert.True(page3.Rooms.HasPreviousPage);
            Assert.False(page3.Rooms.HasNextPage);
        }
    }
}
