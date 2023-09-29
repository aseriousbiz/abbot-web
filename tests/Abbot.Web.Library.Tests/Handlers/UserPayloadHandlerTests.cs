using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Events;
using Serious.Abbot.PayloadHandlers;
using Xunit;

public class UserPayloadHandlerTests
{
    public class TheOnPlatformEventAsyncMethod
    {
        [Fact]
        public async Task WithUserUpdateMessageCreatesUserAndMemberIfNew()
        {
            var env = TestEnvironment.Create();
            var userEvent = env.CreateFakePlatformEvent(
                new UserEventPayload(
                    "U999",
                    null,
                    "Real New User",
                    "Test New User",
                    "test@example.com",
                    "America/Vancouver",
                    "http://example.com"));
            var handler = env.Activate<UserPayloadHandler>();

            await handler.OnPlatformEventAsync(userEvent);

            var users = await env.Db.Users.Include(u => u.Members).ToListAsync();
            Assert.Collection(users,
                u0 => {
                    Assert.True(u0.IsBot);
                    Assert.True(u0.IsAbbot);
                    Assert.Equal("system|abbot", u0.NameIdentifier);
                },
                u0 => {
                    Assert.True(u0.IsBot);
                    Assert.True(u0.IsAbbot);
                    Assert.Null(u0.NameIdentifier);
                },
                u => Assert.Equal(env.TestData.User.Id, u.Id),
                u => Assert.Equal(env.TestData.GuestUser.Id, u.Id),
                u => Assert.Equal(env.TestData.ForeignUser.Id, u.Id),
                u => {
                    Assert.NotEqual(env.TestData.User.Id, u.Id);
                    Assert.Equal("U999", u.PlatformUserId);
                    Assert.Equal("Test New User", u.DisplayName);
                    Assert.Equal("Real New User", u.RealName);
                    Assert.Equal("test@example.com", u.Email);
                    Assert.Equal("http://example.com", u.Avatar);
                    Assert.Collection(u.Members,
                        m => {
                            Assert.Equal(env.TestData.Organization.Id, m.OrganizationId);
                            Assert.Equal("Test New User", m.DisplayName);
                            Assert.Equal("America/Vancouver", m.TimeZoneId);
                        });
                });
        }

        [Fact]
        public async Task WithUserUpdateMessageFromForeignOrgCreatesMemberInCorrectOrg()
        {
            var env = TestEnvironment.Create();
            var userEvent = env.CreateFakePlatformEvent(
                new UserEventPayload(
                    "U999",
                    env.TestData.ForeignOrganization.PlatformId,
                    "Real New User",
                    "Test New User",
                    "test@example.com",
                    "America/Vancouver",
                    "http://example.com"));
            var handler = env.Activate<UserPayloadHandler>();

            await handler.OnPlatformEventAsync(userEvent);

            var users = await env.Db.Users.Where(u => !u.IsBot).Include(u => u.Members).ToListAsync();
            Assert.Collection(users,
                u => Assert.Equal(env.TestData.User.Id, u.Id),
                u => Assert.Equal(env.TestData.GuestUser.Id, u.Id),
                u => Assert.Equal(env.TestData.ForeignUser.Id, u.Id),
                u => {
                    Assert.NotEqual(env.TestData.User.Id, u.Id);
                    Assert.Equal("U999", u.PlatformUserId);
                    Assert.Equal("Test New User", u.DisplayName);
                    Assert.Equal("Real New User", u.RealName);
                    Assert.Null(u.Email);
                    Assert.Equal("http://example.com", u.Avatar);
                    Assert.Collection(u.Members,
                        m => {
                            Assert.Equal(env.TestData.ForeignOrganization.Id, m.OrganizationId);
                            Assert.Equal("Test New User", m.DisplayName);
                            Assert.Equal("America/Vancouver", m.TimeZoneId);
                        });
                });
        }

        [Fact]
        public async Task WithUserUpdateMessageFromForeignOrgIgnoresItIfOrgDoesNotAlreadyExist()
        {
            var env = TestEnvironment.Create();
            var userEvent = env.CreateFakePlatformEvent(
                new UserEventPayload(
                    NonExistent.PlatformUserId,
                    NonExistent.PlatformId,
                    "Real New User",
                    "Test New User",
                    "test@example.com",
                    "America/Vancouver",
                    "http://example.com"));
            var handler = env.Activate<UserPayloadHandler>();

            await handler.OnPlatformEventAsync(userEvent);
            var users = await env.Db.Users.Where(u => u.PlatformUserId == NonExistent.PlatformUserId)
                .Include(u => u.Members).ToListAsync();

            Assert.Empty(users);

            var members = await env.Db.Members.Where(m => m.User.PlatformUserId == NonExistent.PlatformUserId)
                .Include(m => m.User).ToListAsync();

            Assert.Empty(members);
        }
    }
}
