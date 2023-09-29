using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Settings.Organization;
using Serious.Abbot.Pages.Settings.Rooms;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Filters;

public class RoomsIndexPageTests
{
    public class TheOnGetAsyncMethod : PageTestBase<RoomsIndexPage>
    {
        [Theory]
        [InlineData(TrackStateFilter.Tracked, "af")]   // We filter out rooms without bot to the top for this state.
        [InlineData(TrackStateFilter.Untracked, "g")]  // We filter out rooms without bot to the top for this state.
        [InlineData(TrackStateFilter.Inactive, "ed")]
        [InlineData(TrackStateFilter.BotMissing, "bc")]
        public async Task LoadsPersistentRoomsForTrackState(TrackStateFilter tab, string expected)
        {
            await Env.CreateRoomAsync(name: "f", managedConversationsEnabled: true, botIsMember: true);
            await Env.CreateRoomAsync(name: "e", managedConversationsEnabled: true, deleted: true);
            await Env.CreateRoomAsync(name: "d", managedConversationsEnabled: true, archived: true);
            await Env.CreateRoomAsync(name: "a", managedConversationsEnabled: true, botIsMember: true);
            await Env.CreateRoomAsync(name: "b", managedConversationsEnabled: true, botIsMember: false);
            await Env.CreateRoomAsync(name: "c", managedConversationsEnabled: false, botIsMember: false);
            await Env.CreateRoomAsync(name: "g", managedConversationsEnabled: false, botIsMember: true);

            var (page, result) = await InvokePageAsync(p => {
                p.Tab = tab;
                return p.OnGetAsync();
            });

            Assert.IsType<PageResult>(result);
            Assert.Equal(expected.ToCharArray(), page.Rooms.Select(r => r.Name![0]));
        }

        [Fact]
        public async Task ReturnsNotFoundForUndefined()
        {
            var (_, result) = await InvokePageAsync(p => {
                p.Tab = (TrackStateFilter)42;
                return p.OnGetAsync();
            });
            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData(0, 2, TrackStateFilter.Tracked, null, false, false, false)]
        [InlineData(0, -1, TrackStateFilter.Untracked, null, false, true, false)]
        [InlineData(0, 0, TrackStateFilter.Tracked, "filter", false, false, true)]
        [InlineData(0, 2, TrackStateFilter.Untracked, "filter", false, true, true)]
        [InlineData(2, 3, TrackStateFilter.Untracked, "filter", true, true, true)]
        public async Task RedirectsToDefaultPageWhenPageOutOfRangeAndNoRooms(
            int pages,
            int pageNumber,
            TrackStateFilter tab,
            string? filter,
            bool pageExists,
            bool tabExists,
            bool filterExists)
        {
            if (pages > 1)
            {
                // Need to create pages of data.
                for (int i = 0; i < pages; i++)
                {
                    await Env.CreateRoomAsync(name: $"filter_{i}", managedConversationsEnabled: tab == TrackStateFilter.Tracked, botIsMember: true);
                }
            }

            await Env.Settings.SetIntegerValueAsync(
                SettingsScope.Member(Env.TestData.Member),
                "Rooms:PageSize",
                1,
                Env.TestData.User);

            var filterList = filter is null
                ? new FilterList()
                : FilterParser.Parse(filter);
            var (_, result) = await InvokePageAsync(p => {
                p.Tab = tab;
                p.Filter = filterList;
                p.PageNumber = pageNumber;
                return p.OnGetAsync();
            });
            var redirectResult = Assert.IsType<RedirectToPageResult>(result);
            Assert.NotNull(redirectResult.RouteValues);
            var routeValues = redirectResult.RouteValues;
            Assert.Equal(pageExists, routeValues.ContainsKey("p"));
            if (pageExists)
            {
                Assert.Equal(pages, routeValues["p"]);
            }
            Assert.Equal(tabExists, routeValues.ContainsKey("tab"));
            if (tabExists)
            {
                Assert.Equal(tab, routeValues["tab"]);
            }
            Assert.Equal(filterExists, routeValues.ContainsKey("q"));
            if (filterExists)
            {
                Assert.Equal(filterList.ToString(), routeValues["q"]);
            }
        }

        [Theory]
        [InlineData(TrackStateFilter.Tracked, new[] { "cool-hand-luke", "cool-runnings" })]
        [InlineData(TrackStateFilter.Untracked, new[] { "the-cool-room" })]
        [InlineData(TrackStateFilter.Inactive, new[] { "be cool" })]
        [InlineData(TrackStateFilter.BotMissing, new[] { "cool-dog" })]
        public async Task CanFilterByNameAndTracked(TrackStateFilter tab, string[] expectedPages)
        {
            await Env.CreateRoomAsync(name: "be cool", archived: true);
            await Env.CreateRoomAsync(name: "the-danger-room", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "the-cool-room", managedConversationsEnabled: false);
            await Env.CreateRoomAsync(name: "the-boring-room", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "cool-hand-luke", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "cool-runnings", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "cool-dog", managedConversationsEnabled: true, botIsMember: false);

            // Load the rooms
            var (page, _) = await InvokePageAsync(p => {
                p.Filter = new FilterList { new("cool") };
                p.Tab = tab;
                return p.OnGetAsync();
            });

            // Check the result
            Assert.Equal("cool", page.Filter.ToString());
            Assert.Equal(
                expectedPages,
                page.Rooms.Select(r => r.Name).ToArray());
        }

        [Fact]
        public async Task CanPaginateWithPreviousNext()
        {
            for (int i = 0; i < 21; i++)
            {
                // Create rooms where half have conversations enabled and half do not (just to make sure we bring them all back)
                await Env.CreateRoomAsync(name: $"r-{i:00}", managedConversationsEnabled: true);
            }

            // Load the rooms
            var (page1, _) = await InvokePageAsync(p => {
                p.PageNumber = 1;
                return p.OnGetAsync();
            });
            var (page2, _) = await InvokePageAsync(p => {
                p.PageNumber = 2;
                return p.OnGetAsync();
            });
            var (page3, _) = await InvokePageAsync(p => {
                p.PageNumber = 3;
                return p.OnGetAsync();
            });
            var (page4, _) = await InvokePageAsync(p => {
                p.PageNumber = 4;
                return p.OnGetAsync();
            });

            // Check the result
            Assert.Equal(new[] { "r-00", "r-01", "r-02", "r-03", "r-04", "r-05", "r-06", "r-07", "r-08", "r-09" },
                page1.Rooms.Select(r => r.Name).ToArray());

            Assert.False(page1.Rooms.HasPreviousPage);
            Assert.True(page1.Rooms.HasNextPage);
            Assert.Equal(new[] { "r-10", "r-11", "r-12", "r-13", "r-14", "r-15", "r-16", "r-17", "r-18", "r-19" },
                page2.Rooms.Select(r => r.Name).ToArray());

            Assert.True(page2.Rooms.HasPreviousPage);
            Assert.True(page2.Rooms.HasNextPage);
            Assert.Equal(new[] { "r-20" },
                page3.Rooms.Select(r => r.Name).ToArray());
            Assert.True(page3.Rooms.HasPreviousPage);
            Assert.False(page3.Rooms.HasNextPage);
            Assert.Empty(page4.Rooms);
            Assert.True(page3.Rooms.HasPreviousPage);
            Assert.False(page3.Rooms.HasNextPage);
        }

        [Theory]
        [InlineData("danger", new[] { "the-danger-room" })]
        [InlineData("the", new[] { "the-danger-room", "the-ops-room" })]
        [InlineData("r", new[] { "random", "the-danger-room", "the-ops-room" })]
        public async Task SupportsFiltering(string? filter, string[] expectedRoomTitles)
        {
            // Create test rooms
            await Env.CreateRoomAsync(name: "danger-zone", managedConversationsEnabled: false);
            await Env.CreateRoomAsync(name: "the-danger-room", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "the-ops-room", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "yelling", managedConversationsEnabled: true);
            await Env.CreateRoomAsync(name: "random", managedConversationsEnabled: true);

            // Run the page
            var (page, _) = await InvokePageAsync(p => {
                p.Filter = filter is null ? new FilterList() : new FilterList { new(filter) };
                return p.OnGetAsync();
            });
            Assert.Equal(filter, page.Filter.ToString());
            Assert.Equal(expectedRoomTitles, page.Rooms.Select(r => r.Name).ToArray());
        }

        [Theory]
        [InlineData(1, WebConstants.ShortPageSize)]
        [InlineData(2, WebConstants.ShortPageSize)]
        [InlineData(3, WebConstants.ShortPageSize / 2)]
        public async Task CanPaginate(int pageNumber, int recordCount)
        {
            // Create 2 full pages and 1 half-page of rooms
            var roomCount = WebConstants.ShortPageSize * 2 + WebConstants.ShortPageSize / 2;
            for (var i = 0; i < roomCount; i++)
            {
                await Env.CreateRoomAsync(name: $"r-{i:00}");
            }

            // Run the page for the
            var (page, _) = await InvokePageAsync(p => {
                p.Tab = TrackStateFilter.Untracked;
                p.PageNumber = pageNumber;
                return p.OnGetAsync();
            });

            Assert.Collection(page.Rooms, Enumerable.Range(0, recordCount).Select(i => {
                return new Action<Room>(r => {
                    var idx = ((pageNumber - 1) * WebConstants.ShortPageSize) + i;
                    Assert.Equal($"r-{idx:00}", r.Name);
                });
            }).ToArray());
        }

        [Fact]
        public async Task PullsDefaultMessageSettingsIfNoOrganizationSettings()
        {
            var (page, _) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.Equal(RoomSettings.Default.ConversationWelcomeMessage, page.UpdateMessagesInput.ConversationWelcomeMessage);
            Assert.Equal(RoomSettings.Default.UserWelcomeMessage, page.UpdateMessagesInput.UserWelcomeMessage);
            Assert.Equal(RoomSettings.Default.WelcomeNewConversations, page.UpdateMessagesInput.WelcomeNewConversations);
            Assert.Equal(RoomSettings.Default.WelcomeNewUsers, page.UpdateMessagesInput.WelcomeNewUsers);
        }

        [Fact]
        public async Task PullsOrganizationMessageSettingsIfSpecified()
        {
            TestOrganization.DefaultRoomSettings = new()
            {
                WelcomeNewConversations = true,
                ConversationWelcomeMessage = "You got problem?",
                WelcomeNewUsers = true,
                UserWelcomeMessage = "Welcome to Room!",
            };

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.Equal("You got problem?", page.UpdateMessagesInput.ConversationWelcomeMessage);
            Assert.Equal("Welcome to Room!", page.UpdateMessagesInput.UserWelcomeMessage);
            Assert.True(page.UpdateMessagesInput.WelcomeNewConversations);
            Assert.True(page.UpdateMessagesInput.WelcomeNewUsers);
        }

        [Fact]
        public async Task UsesGlobalDefaultsForAnythingNotSpecifiedInOrgSettings()
        {
            TestOrganization.DefaultRoomSettings = new()
            {
                WelcomeNewConversations = true,
                ConversationWelcomeMessage = "You got problem?",
            };

            var (page, _) = await InvokePageAsync(p => p.OnGetAsync());

            Assert.Equal("You got problem?", page.UpdateMessagesInput.ConversationWelcomeMessage);
            Assert.Equal(RoomSettings.Default.UserWelcomeMessage, page.UpdateMessagesInput.UserWelcomeMessage);
            Assert.True(page.UpdateMessagesInput.WelcomeNewConversations);
            Assert.Equal(RoomSettings.Default.WelcomeNewUsers, page.UpdateMessagesInput.WelcomeNewUsers);
        }
    }

    public class TheOnPostUpdateMessagesAsyncMethod : PageTestBase<RoomsIndexPage>
    {
        [Fact]
        public async Task RedirectsWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var (page, result) = await InvokePageAsync(p => p.OnPostUpdateMessagesAsync(new(false, "", false, "")));
            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("You must upgrade your plan to use this feature.", page.StatusMessage);
        }

        [Fact]
        public async Task UpdatesOrganizationSettingsWithProvidedValues()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Administrator, Env.TestData.Abbot);

            var (page, result) = await InvokePageAsync(p => p.OnPostUpdateMessagesAsync(new(
                true,
                "Welcome to Room!",
                true,
                "You got problem?")));

            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("Updated auto-responder settings.", page.StatusMessage);

            await Env.ReloadAsync(TestOrganization);
            Assert.Equal("Welcome to Room!", TestOrganization.DefaultRoomSettings?.UserWelcomeMessage);
            Assert.Equal("You got problem?", TestOrganization.DefaultRoomSettings?.ConversationWelcomeMessage);
            Assert.True(TestOrganization.DefaultRoomSettings?.WelcomeNewUsers);
            Assert.True(TestOrganization.DefaultRoomSettings?.WelcomeNewConversations);
        }

        [Fact]
        public async Task ExplicitlySetsNullForValuesNotSet()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Administrator, Env.TestData.Abbot);

            var (page, result) = await InvokePageAsync(p => p.OnPostUpdateMessagesAsync(new(
                true,
                null,
                true,
                null)));

            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("Updated auto-responder settings.", page.StatusMessage);

            await Env.ReloadAsync(TestOrganization);
            Assert.Null(TestOrganization.DefaultRoomSettings?.UserWelcomeMessage);
            Assert.Null(TestOrganization.DefaultRoomSettings?.ConversationWelcomeMessage);
            Assert.True(TestOrganization.DefaultRoomSettings?.WelcomeNewUsers);
            Assert.True(TestOrganization.DefaultRoomSettings?.WelcomeNewConversations);
        }
    }

    public class TheOnPostAssignAsyncMethod : PageTestBase<RoomsIndexPage>
    {
        [Fact]
        public async Task ReturnsTurboFlashWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var (_, result) = await InvokePageAsync(
                p => p.OnPostAssignAsync(Env.TestData.Member, RoomRole.FirstResponder),
                acceptsTurbo: true);
            Assert.NotNull(result);
            result.AssertTurboFlashMessage("You must upgrade your plan to use this feature.");
        }
    }

    public class TheOnPostSaveRoomsResponseTimesAsyncMethod : PageTestBase<RoomsIndexPage>
    {
        [Fact]
        public async Task RedirectsWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var (page, result) = await InvokePageAsync(p => p.OnPostSaveRoomsResponseTimesAsync());
            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("You must upgrade your plan to use this feature.", page.StatusMessage);
        }

        [Fact]
        public async Task SetsCustomResponseTimesForSelectedRooms()
        {
            var room1 = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            var room2 = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            var room3 = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Administrator, Env.TestData.Abbot);

            var (_, result) = await InvokePageAsync(async page => {
                page.RoomIds = new[] { room1.PlatformRoomId, room3.PlatformRoomId };
                page.ResponseTimeSettings = ResponseTimeSettings.FromTimeToRespond(
                    new Threshold<TimeSpan>(TimeSpan.FromHours(3), TimeSpan.FromHours(5)),
                    readOnly: false,
                    useCustomResponseTimes: true);
                return await page.OnPostSaveRoomsResponseTimesAsync();
            });

            Assert.IsType<RedirectToPageResult>(result);
            await Env.ReloadAsync(room1, room2, room3);
            Assert.Equal((TimeSpan.FromHours(3), TimeSpan.FromHours(5)), (room1.TimeToRespond.Warning, room1.TimeToRespond.Deadline));
            Assert.Equal((null, null), (room2.TimeToRespond.Warning, room2.TimeToRespond.Deadline));
            Assert.Equal((TimeSpan.FromHours(3), TimeSpan.FromHours(5)), (room3.TimeToRespond.Warning, room3.TimeToRespond.Deadline));
        }
    }

    public class TheOnPostSaveRespondersAsyncMethod : PageTestBase<RoomsIndexPage>
    {
        [Fact]
        public async Task RedirectsWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var (page, result) = await InvokePageAsync(p => p.OnPostSaveRespondersAsync());
            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("You must upgrade your plan to use this feature.", page.StatusMessage);
        }

        [Theory]
        [InlineData(RoomRole.FirstResponder)]
        [InlineData(RoomRole.EscalationResponder)]
        public async Task SavesRespondersForSelectedRooms(RoomRole roomRole)
        {
            var room1 = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            var room2 = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            var room3 = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            var member1 = await Env.CreateMemberInAgentRoleAsync();
            var member2 = await Env.CreateMemberInAgentRoleAsync();
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Administrator, Env.TestData.Abbot);

            var (_, result) = await InvokePageAsync(async page => {
                page.RoomRole = roomRole;
                page.RoomIds = new[] { room1.PlatformRoomId, room3.PlatformRoomId };
                page.ResponderIds = new[] { member1.User.PlatformUserId, member2.User.PlatformUserId };
                return await page.OnPostSaveRespondersAsync();
            });

            Assert.IsType<RedirectToPageResult>(result);
            await Env.ReloadAsync(room1, room2, room3);
            var room1Responders = room1.Assignments.Where(a => a.Role == roomRole).Select(a => a.MemberId);
            var room1Others = room1.Assignments.Where(a => a.Role != roomRole).Select(a => a.Member);
            var room2Responders = room2.Assignments.Where(a => a.Role == roomRole).Select(a => a.MemberId);
            var room2Others = room2.Assignments.Where(a => a.Role != roomRole).Select(a => a.Member);
            var room3Responders = room3.Assignments.Where(a => a.Role == roomRole).Select(a => a.MemberId);
            var room3Others = room3.Assignments.Where(a => a.Role != roomRole).Select(a => a.Member);
            Assert.Collection(room1Responders,
                responderId => Assert.Equal(member1.Id, responderId),
                responderId => Assert.Equal(member2.Id, responderId));
            Assert.Empty(room1Others);
            Assert.Empty(room2Responders);
            Assert.Empty(room2Others);
            Assert.Collection(room3Responders,
                responderId => Assert.Equal(member1.Id, responderId),
                responderId => Assert.Equal(member2.Id, responderId));
            Assert.Empty(room3Others);
        }
    }

    public class TheOnPostTrackConversationsAsyncMethod : PageTestBase<RoomsIndexPage>
    {
        [Fact]
        public async Task RedirectsWithMessageIfConversationTrackingNotAvailableInOrganization()
        {
            Env.TestData.Organization.PlanType = PlanType.Free;
            await Env.Db.SaveChangesAsync();

            var (page, result) = await InvokePageAsync(p => p.OnPostTrackConversationsAsync());
            Assert.IsType<RedirectToPageResult>(result);
            Assert.Equal("You must upgrade your plan to use this feature.", page.StatusMessage);
        }

        [Fact]
        public async Task EnablesConversationTrackingForSelectedRooms()
        {
            var room1 = await Env.CreateRoomAsync(managedConversationsEnabled: false);
            var room2 = await Env.CreateRoomAsync(managedConversationsEnabled: false);
            var room3 = await Env.CreateRoomAsync(managedConversationsEnabled: false);
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Administrator, Env.TestData.Abbot);

            var (_, result) = await InvokePageAsync(async page => {
                page.RoomIds = new[] { room1.PlatformRoomId, room3.PlatformRoomId };
                return await page.OnPostTrackConversationsAsync();
            });

            Assert.IsType<RedirectToPageResult>(result);
            await Env.ReloadAsync(room1, room2, room3);
            Assert.True(room1.ManagedConversationsEnabled);
            Assert.False(room2.ManagedConversationsEnabled);
            Assert.True(room3.ManagedConversationsEnabled);
        }
    }
}
