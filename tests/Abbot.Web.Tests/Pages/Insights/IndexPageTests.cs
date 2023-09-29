using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Api;
using Serious.Abbot.Pages.Insights;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

public class IndexPageTests : PageTestBase<IndexPage>
{
    public class TheOnGetAsyncMethod : IndexPageTests
    {
        [Fact]
        public async Task InitializesDefaultMemberFilterAndRangeValues()
        {
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Agent, Env.TestData.Abbot);

            var (page, _) = await InvokePageAsync<PageResult>(p => p.OnGetAsync());

            Assert.Equal(InsightsRoomFilter.Yours, page.Input.SelectedFilter);
            Assert.Equal(DateRangeOption.Week, page.Input.SelectedRange);
            Assert.Collection(page.FilterOptions,
                option0 => Assert.Equal(InsightsRoomFilter.Yours, option0.Value));
        }

        [Fact]
        public async Task InitializesAdminFilterAndPassedInRange()
        {
            Builder.ReplaceService<IInsightsRepository, InsightsRepository>();
            var room = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            await Env.CreateRoomAsync(managedConversationsEnabled: false);
            var room2 = await Env.CreateRoomAsync(managedConversationsEnabled: true);
            var otherUser = await Env.CreateMemberInAgentRoleAsync();
            await Env.Rooms.AssignMemberAsync(room, otherUser, RoomRole.FirstResponder, Env.TestData.Member);
            await Env.Roles.AddUserToRoleAsync(Env.TestData.Member, Roles.Administrator, Env.TestData.Abbot);

            var (page, _) = await InvokePageAsync<PageResult>(p => p.OnGetAsync(range: DateRangeOption.Month));

            Assert.Equal(InsightsRoomFilter.All, page.Input.SelectedFilter);
            Assert.Equal(DateRangeOption.Month, page.Input.SelectedRange);
            Assert.Collection(page.FilterOptions,
                o => Assert.Equal(InsightsRoomFilter.All, o.Value),
                o => Assert.Equal(InsightsRoomFilter.Yours, o.Value),
                o => Assert.Equal(otherUser.User.PlatformUserId, o.Value),
                o => Assert.Equal(room.PlatformRoomId, o.Value),
                o => Assert.Equal(room2.PlatformRoomId, o.Value));
        }

        [Fact]
        public async Task InitializesSpecifiedFilterAndRangeValues()
        {
            var (page, _) = await InvokePageAsync<PageResult>(p => p.OnGetAsync(filter: InsightsRoomFilter.All, DateRangeOption.Year));

            Assert.Equal(InsightsRoomFilter.All, page.Input.SelectedFilter);
            Assert.Equal(DateRangeOption.Year, page.Input.SelectedRange);
        }
    }
}
