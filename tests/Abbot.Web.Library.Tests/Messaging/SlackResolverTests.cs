using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Refit;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Slack;
using Xunit;

public class SlackResolverTests
{
    public class TheResolveRoomsAsyncMethod
    {
        [Fact]
        public async Task RetrievesExistingRooms()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var room1 = await env.CreateRoomAsync();
            var room2 = await env.CreateRoomAsync();
            var channelIds = new[] { room1.PlatformRoomId, room2.PlatformRoomId };
            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveRoomsAsync(channelIds, organization, false);

            Assert.Collection(result,
                r => Assert.Equal(room1.PlatformRoomId, r.PlatformRoomId),
                r => Assert.Equal(room2.PlatformRoomId, r.PlatformRoomId));
        }

        [Fact]
        public async Task CreatesRoomsFromSlackApi()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var organization = env.TestData.Organization;
            var conversation1Info = new ConversationInfo
            {
                Name = "the-room",
                Id = "C0000000001",
                IsChannel = true,
                IsGroup = false,
                IsInstantMessage = false,
                IsMultipartyInstantMessage = false,
                IsArchived = false,
                IsMember = true,
            };
            var conversation2Info = new ConversationInfo
            {
                Name = "another-room",
                Id = "C0000000002",
                IsChannel = true,
                IsGroup = false,
                IsInstantMessage = false,
                IsMultipartyInstantMessage = false,
                IsArchived = false,
                IsMember = true,
            };
            env.SlackApi.Conversations.AddConversationInfoResponse("xoxb-this-is-a-test-token", conversation1Info);
            env.SlackApi.Conversations.AddConversationInfoResponse("xoxb-this-is-a-test-token", conversation2Info);
            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveRoomsAsync(
                new[] { "C0000000001", "C0000000002" },
                organization,
                false);

            Assert.Collection(result,
                r => {
                    Assert.Equal("C0000000001", r.PlatformRoomId);
                    Assert.Equal(env.Clock.UtcNow, r.LastPlatformUpdate);
                },
                r => {
                    Assert.Equal("C0000000002", r.PlatformRoomId);
                    Assert.Equal(env.Clock.UtcNow, r.LastPlatformUpdate);
                });
        }
    }

    public class TheResolveRoomAsyncMethod
    {
        [Fact]
        public async Task RetrievesExistingRoom()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync();
            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveRoomAsync(room.PlatformRoomId, organization, false);

            Assert.NotNull(result);
            Assert.Equal(room.PlatformRoomId, result.PlatformRoomId);
            var rooms = await env.Db.Rooms.ToListAsync();
            Assert.Single(rooms);
        }

        [Theory]
        [InlineData(true, false, false, false, false, false, RoomType.PublicChannel)]
        [InlineData(true, false, false, true, false, true, RoomType.MultiPartyDirectMessage)]
        [InlineData(true, false, true, false, false, true, RoomType.DirectMessage)]
        [InlineData(true, true, false, false, false, false, RoomType.PrivateChannel)]
        [InlineData(true, false, false, false, true, false, RoomType.PublicChannel)]
        [InlineData(true, false, false, false, false, true, RoomType.PublicChannel)]
        public async Task CreatesRoomFromSlackApi(bool isChannel, bool isGroup, bool isIm, bool isMpdm, bool isArchived,
            bool isMember, RoomType expectedRoomType)
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();

            var organization = env.TestData.Organization;
            var conversationInfo = new ConversationInfo
            {
                Name = "the-room",
                Id = "C01234",
                IsChannel = isChannel,
                IsGroup = isGroup,
                IsInstantMessage = isIm,
                IsMultipartyInstantMessage = isMpdm,
                IsArchived = isArchived,
                IsMember = isMember,
            };

            env.SlackApi.Conversations.AddConversationInfoResponse("xoxb-this-is-a-test-token", conversationInfo);
            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveRoomAsync("C01234", organization, false);

            Assert.NotNull(result);
            Assert.Equal("C01234", result.PlatformRoomId);
            Assert.Equal("the-room", result.Name);
            var rooms = await env.Db.Rooms.ToListAsync();
            var room = Assert.Single(rooms);
            Assert.Equal("C01234", room.PlatformRoomId);
            Assert.Equal(env.Clock.UtcNow, room.LastPlatformUpdate);
            Assert.Equal(isArchived, room.Archived);
            Assert.Equal(isMember, room.BotIsMember);
            Assert.Equal(expectedRoomType, room.RoomType);
        }

        [Theory]
        [InlineData(null, true, RoomType.PublicChannel, true, false, true, RoomType.MultiPartyDirectMessage)]
        [InlineData(true, null, RoomType.PublicChannel, true, false, true, RoomType.MultiPartyDirectMessage)]
        [InlineData(true, true, RoomType.Unknown, true, false, true, RoomType.MultiPartyDirectMessage)]
        public async Task UpdatesIncompleteRoomFromSlackApi(
            bool? currentArchived, bool? currentBotIsMember, RoomType currentRoomType,
            bool isMpdm, bool isArchived, bool isMember, RoomType expectedRoomType)
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();

            var organization = env.TestData.Organization;
            var lastUpdate = env.Clock.UtcNow.AddDays(-1);
            var room = new Room
            {
                PlatformRoomId = "C01234",
                Name = "the-room",
                RoomType = currentRoomType,
                BotIsMember = currentBotIsMember,
                Archived = currentArchived,
                LastPlatformUpdate = lastUpdate,
                Organization = organization
            };

            await env.Rooms.CreateAsync(room);

            var conversationInfo = new ConversationInfo
            {
                Name = "the-room",
                Id = "C01234",
                IsMultipartyInstantMessage = isMpdm,
                IsArchived = isArchived,
                IsMember = isMember,
            };

            env.SlackApi.Conversations.AddConversationInfoResponse("xoxb-this-is-a-test-token", conversationInfo);
            var slackResolver = env.Activate<SlackResolver>();

            await slackResolver.ResolveRoomAsync("C01234", organization, false);

            await env.ReloadAsync(room);
            Assert.Equal(env.Clock.UtcNow, room.LastPlatformUpdate);
            Assert.Equal(isArchived, room.Archived);
            Assert.Equal(isMember, room.BotIsMember);
            Assert.Equal(expectedRoomType, room.RoomType);
        }

        [Fact]
        public async Task UpdatesCompleteRoomWhenForceRefreshSpecified()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();

            var organization = env.TestData.Organization;
            var lastUpdate = env.Clock.UtcNow.AddDays(-1);
            var room = new Room
            {
                PlatformRoomId = "C01234",
                Name = "the-room",
                RoomType = RoomType.PublicChannel,
                BotIsMember = true,
                Archived = false,
                LastPlatformUpdate = lastUpdate,
                Organization = organization
            };

            await env.Rooms.CreateAsync(room);

            var conversationInfo = new ConversationInfo
            {
                Name = "the-room",
                Id = "C01234",
                IsMultipartyInstantMessage = false,
                IsArchived = true,
                IsMember = false,
                IsGroup = true,
            };

            env.SlackApi.Conversations.AddConversationInfoResponse("xoxb-this-is-a-test-token", conversationInfo);
            var slackResolver = env.Activate<SlackResolver>();

            await slackResolver.ResolveRoomAsync("C01234", organization, true);

            await env.ReloadAsync(room);
            Assert.Equal(env.Clock.UtcNow, room.LastPlatformUpdate);
            Assert.True(room.Archived);
            Assert.False(room.BotIsMember);
            Assert.Equal(RoomType.PrivateChannel, room.RoomType);
        }

        [Fact]
        public async Task ReturnsNullIfChannelDoesNotExistOnSlack()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();

            var organization = env.TestData.Organization;

            env.SlackApi.Conversations.AddConversationInfoResponse("xoxb-this-is-a-test-token",
                "CDOESNOTEXIST",
                "channel_not_found");

            var slackResolver = env.Activate<SlackResolver>();

            var room = await slackResolver.ResolveRoomAsync("CDOESNOTEXIST", organization, true);

            Assert.Null(room);
        }

        [Fact]
        public async Task MarksExistingRoomDeletedIfChannelNoLongerExists()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();

            var organization = env.TestData.Organization;
            var lastUpdate = env.Clock.UtcNow.AddDays(-1);
            var room = new Room
            {
                PlatformRoomId = "C01234",
                Name = "the-room",
                RoomType = RoomType.PublicChannel,
                BotIsMember = true,
                Archived = false,
                Deleted = false,
                LastPlatformUpdate = lastUpdate,
                Organization = organization
            };

            await env.Rooms.CreateAsync(room);

            env.SlackApi.Conversations.AddConversationInfoResponse("xoxb-this-is-a-test-token",
                room.PlatformRoomId,
                "channel_not_found");

            var slackResolver = env.Activate<SlackResolver>();

            await slackResolver.ResolveRoomAsync("C01234", organization, true);

            await env.ReloadAsync(room);
            Assert.Equal(env.Clock.UtcNow, room.LastPlatformUpdate);
            Assert.True(room.Deleted);
        }

        [Fact]
        public async Task ThrowsExceptionWhenRoomNotFound()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var slackResolver = env.Activate<SlackResolver>();

            var result = await Assert.ThrowsAsync<InvalidOperationException>(
                () => slackResolver.ResolveRoomAsync("C01234", organization, false));

            Assert.Equal(
                $"Error retrieving room `C01234` for org `{organization.PlatformId}`\n Error: not_found\n",
                result.Message);

            var rooms = await env.Db.Rooms.ToListAsync();
            Assert.Empty(rooms);
        }
    }

    public class TheResolveAndUpdateRoomAsyncMethod
    {
        [Fact]
        public async Task UpdatesRoomFromPartialRoomInfo()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync(botIsMember: true);
            var channelInfo = new ConversationInfoItem
            {
                Id = room.PlatformRoomId,
                Name = "new-name",
            };
            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveAndUpdateRoomAsync(channelInfo, organization);

            Assert.NotNull(result);
            Assert.Equal(room.PlatformRoomId, result.PlatformRoomId);
            Assert.Equal("new-name", room.Name);
            Assert.True(room.BotIsMember); // Should be unchanged.
        }
    }

    public class TheResolveOrganizationAsyncMethod
    {
        [Fact]
        public async Task ReturnsExistingHomeNonEnterpriseOrganizationAndUpdatesEnterpriseGridId()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamInfo = new TeamInfo
            {
                Id = organization.PlatformId,
                Name = "The new org name.",
                Icon = new Icon(),
            };
            env.SlackApi.AddTeamInfo(organization.ApiToken!.Reveal(), organization.PlatformId, teamInfo);

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationAsync(
                organization.PlatformId,
                organization);

            Assert.Same(organization, result);
            Assert.Equal(string.Empty, organization.EnterpriseGridId);
        }

        [Fact]
        public async Task ReturnsExistingHomeEnterpriseOrganizationAndUpdatesEnterpriseGridId()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamInfo = new TeamInfo
            {
                Id = organization.PlatformId,
                Name = "The new org name.",
                EnterpriseId = "E00001211",
                Icon = new Icon(),
            };
            env.SlackApi.AddTeamInfo(organization.ApiToken!.Reveal(), organization.PlatformId, teamInfo);

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationAsync(
                organization.PlatformId,
                organization);

            Assert.Same(organization, result);
            Assert.Equal("E00001211", organization.EnterpriseGridId);
        }

        [Fact]
        public async Task CreatesHomeEnterpriseOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamInfo = new TeamInfo
            {
                Id = "T000001212",
                EnterpriseId = "E001234113",
                Name = "The new org name.",
                Icon = new Icon(),
            };
            env.SlackApi.AddTeamInfo(organization.ApiToken!.Reveal(), "T000001212", teamInfo);

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationAsync(
                "T000001212",
                organization);

            Assert.NotEqual(organization.Id, result.Id);
            Assert.Equal("T000001212", result.PlatformId);
            Assert.Equal("E001234113", result.EnterpriseGridId);
        }

        [Fact]
        public async Task CreatesForeignOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamInfo = new TeamInfo
            {
                Id = "T000001212",
                Name = "The new org name.",
                Icon = new Icon(),
            };
            env.SlackApi.AddTeamInfo(organization.ApiToken!.Reveal(), "T000001212", teamInfo);

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationAsync(
                "T000001212",
                organization);

            Assert.NotEqual(organization.Id, result.Id);
            Assert.Equal("T000001212", result.PlatformId);
            Assert.Equal(string.Empty, result.EnterpriseGridId);
        }

        [Fact]
        public async Task CreatesForeignEnterpriseOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var teamInfo = new TeamInfo
            {
                Id = "E000001212",
                Name = "The new org name.",
                Icon = new Icon(),
            };
            env.SlackApi.AddTeamInfo(organization.ApiToken!.Reveal(), "E000001212", teamInfo);
            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationAsync(
                "E000001212",
                organization);

            Assert.NotEqual(organization.Id, result.Id);
            // Until we separate `Organization` from `Workspace`, this is the current behavior.
            Assert.Equal("E000001212", result.PlatformId);
            Assert.Equal("E000001212", result.EnterpriseGridId);
        }
    }

    public class TheResolveOrganizationForUserAsyncMethod
    {
        [Fact]
        public async Task ReturnsSameOrganizationWhenUserInCurrentOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = new UserIdentifier
            {
                Id = env.TestData.User.PlatformUserId,
                TeamId = env.TestData.Organization.PlatformId
            };

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationForUserAsync(user, organization);

            Assert.Same(organization, result);
        }

        [Fact]
        public async Task ReturnsExistingForeignOrganizationForUser()
        {
            var env = TestEnvironment.Create();
            var foreignOrganization = env.TestData.Organization;
            var organization = await env.CreateOrganizationAsync();
            var user = new UserIdentifier
            {
                Id = env.TestData.User.PlatformUserId,
                TeamId = env.TestData.Organization.PlatformId
            };

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationForUserAsync(user, organization);

            Assert.Equal(foreignOrganization.Id, result.Id);
            Assert.NotEqual(result.Id, organization.Id);
        }

        [Fact]
        public async Task CreatesForeignOrganizationForUserWithInfoFromSlackApi()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.SlackApi.AddTeamInfo(organization.ApiToken!.Reveal(),
                "T08675309",
                new TeamInfo
                {
                    Id = "T08675309",
                    Name = "Cool Org",
                    Domain = "org-slug",
                    Icon = new Icon
                    {
                        Image68 = "https://example.com/avatar.png"
                    }
                });

            var user = new UserIdentifier
            {
                Id = env.TestData.User.PlatformUserId,
                TeamId = "T08675309"
            };

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationForUserAsync(user, organization);

            Assert.Equal("T08675309", result.PlatformId);
            Assert.Equal("org-slug", result.Slug);
            Assert.Equal("org-slug.slack.com", result.Domain);
            Assert.Equal("Cool Org", result.Name);
            Assert.Equal(result.Id, result.Id);
            Assert.Equal(PlanType.None, result.PlanType);
            Assert.NotEqual(result.Id, organization.Id);
        }

        [Theory]
        [InlineData("E08675309", null)]
        [InlineData(null, "E08675309")]
        public async Task CreatesForeignEnterpriseOrganizationForUserWithInfoFromSlackApi(string? enterpriseId, string? enterpriseUserId)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.SlackApi.AddTeamInfo(organization.ApiToken!.Reveal(),
                "E08675309",
                new TeamInfo
                {
                    Domain = "ent-slug",
                    Icon = new Icon
                    {
                        Image68 = "https://example.com/avatar.png"
                    },
                    Id = "E08675309",
                    Name = "The Big Organization"
                });

            var user = new UserIdentifier
            {
                Id = env.TestData.User.PlatformUserId,
                EnterpriseId = enterpriseId,
                EnterpriseUser = enterpriseUserId is not null
                    ? new EnterpriseUser { Id = "id1", EnterpriseId = enterpriseUserId }
                    : null,
            };

            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationForUserAsync(user, organization);

            Assert.Equal("E08675309", result.PlatformId);
            Assert.Equal("E08675309", result.EnterpriseGridId);
            Assert.Equal("ent-slug", result.Slug);
            Assert.Equal("The Big Organization", result.Name);
            Assert.Equal(result.Id, result.Id);
            Assert.Equal(PlanType.None, result.PlanType);
            Assert.NotEqual(result.Id, organization.Id);
        }

        [Fact]
        public async Task CreatesForeignEnterpriseOrganizationForUserWhenSlackApiHasNoInfo()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = new UserIdentifier
            {
                Id = "U00012341",
                TeamId = "E08675309"
            };
            var slackResolver = env.Activate<SlackResolver>();

            var result = await slackResolver.ResolveOrganizationForUserAsync(user, organization);

            Assert.Equal("E08675309", result.EnterpriseGridId);
            Assert.Equal("E08675309", result.PlatformId);
            Assert.Equal("E08675309", result.Slug);
            Assert.Null(result.Domain);
            Assert.Null(result.Name);
            Assert.Equal(result.Id, result.Id);
            Assert.Equal(PlanType.None, result.PlanType);
            Assert.NotEqual(result.Id, organization.Id);
        }
    }

    public class TheResolveMemberAsyncMethod
    {
        [Fact]
        public async Task RetrievesExistingMember()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync(user.PlatformUserId, organization);

            Assert.NotNull(result);
            Assert.Same(env.TestData.Member, result);
        }

        [Theory]
        [InlineData(null, "Real!")]
        [InlineData("", "Real!")]
        [InlineData("Display!", "Display!")]
        public async Task RetrievesExistingMemberAndFixesMissingRealName(string? slackDisplayName, string expectedDisplayName)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var user = env.TestData.User;
            user.RealName = null; // Legacy user
            await env.Db.SaveChangesAsync();
            Assert.Equal(member.DisplayName, user.DisplayName);

            // Might as well also update DisplayName from Slack
            Assert.NotEqual(slackDisplayName, member.DisplayName);

            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = user.PlatformUserId,
                    TeamId = organization.PlatformId,
                    Profile = new UserProfile
                    {
                        DisplayName = slackDisplayName,
                        RealName = "Real!",
                    }
                });

            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync(user.PlatformUserId, organization);

            Assert.NotNull(result);
            Assert.Same(env.TestData.Member, result);

            await env.ReloadAsync(result, result.User);
            Assert.Equal(expectedDisplayName, result.DisplayName);
            Assert.Equal(expectedDisplayName, result.User.DisplayName);
            Assert.Equal("Real!", result.User.RealName);
        }

        [Fact]
        public async Task RetrievesExistingMemberButDoesNotUpdateWhenRequestFails()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var user = env.TestData.User;
            user.DisplayName = "Existing";
            user.RealName = null; // Legacy user
            await env.Db.SaveChangesAsync();
            env.SlackApi.AddUserInfoResponse(
                apiToken: "xoxb-this-is-a-test-token",
                user.PlatformUserId,
                response: new UserInfoResponse { Ok = false, Error = "some_error" });
            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync(user.PlatformUserId, organization);

            Assert.NotNull(result);
            Assert.Same(env.TestData.Member, result);
            await env.ReloadAsync(result, result.User);
            Assert.Equal("Existing", result.DisplayName);
            Assert.Equal("Existing", result.User.DisplayName);
            Assert.Null(result.User.RealName);
        }

        [Fact]
        public async Task RetrievesExistingMemberButDoesNotUpdateWhenApiTokenDoesNotExist()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.ApiToken = null;
            var member = env.TestData.Member;
            var user = env.TestData.User;
            user.DisplayName = "Existing";
            user.RealName = null; // Legacy user
            await env.Db.SaveChangesAsync();
            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync(user.PlatformUserId, organization);

            Assert.NotNull(result);
            Assert.Same(env.TestData.Member, result);
            await env.ReloadAsync(result, result.User);
            Assert.Equal("Existing", result.DisplayName);
            Assert.Equal("Existing", result.User.DisplayName);
            Assert.Null(result.User.RealName);
        }

        [Fact]
        public async Task ReturnsAbbotSystemMemberInCurrentOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var resolver = env.Activate<SlackResolver>();
            Assert.NotNull(organization.PlatformBotUserId);

            var result = await resolver.ResolveMemberAsync(organization.PlatformBotUserId, organization);

            Assert.Same(env.TestData.Abbot, result);
        }

        [Theory]
        [InlineData(false, null, true)]
        [InlineData(false, "", true)]
        [InlineData(false, "system|abbot|U00000099", true)] // Legacy format gets updated
        [InlineData(true, null, true)]
        [InlineData(true, "", true)]
        [InlineData(true, "abbot|slack|Thome-U00000099", false)] // Already correct
        public async Task UpdatesNonSystemAbbotMember(bool isAbbot, string nameIdentifier, bool shouldSave)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var customAbbot = await env.CreateMemberAsync("U00000099", displayName: "custom-abbot");
            customAbbot.User.IsBot = true;
            customAbbot.User.IsAbbot = isAbbot;
            customAbbot.User.NameIdentifier = nameIdentifier;
            organization.PlatformBotUserId = "U00000099";
            var resolver = env.Activate<SlackResolver>();
            Assert.NotNull(organization.PlatformBotUserId);

            var saved = false;
            env.Db.SavedChanges += (_, _) => saved = true;

            var result = await resolver.ResolveMemberAsync("U00000099", organization);

            Assert.NotNull(result);
            Assert.Equal("abbot|slack|Thome-U00000099", result.User.NameIdentifier);
            Assert.Equal("custom-abbot", result.DisplayName);
            Assert.Equal(organization.Id, result.OrganizationId);
            Assert.Equal(shouldSave, saved);
        }

        [Fact]
        public async Task ReturnsAbbotMemberForForeignAbbotMention()
        {
            // Every organization that installs Abbot get a unique Bot Id and Bot User Id for Abbot.
            // We formerly did't create a User record for each of these Abbots (instead sharing a system Abbot user), but now we do.
            var env = TestEnvironment.Create();
            var foreignOrganization = env.TestData.ForeignOrganization;
            Assert.NotNull(foreignOrganization.PlatformBotUserId);
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = foreignOrganization.PlatformBotUserId,
                    TeamId = foreignOrganization.PlatformId,
                    IsBot = true,
                    Profile = new UserProfile
                    {
                        RealName = "le abbot"
                    }
                });

            var organization = env.TestData.Organization;
            var resolver = env.Activate<SlackResolver>();
            Assert.NotNull(organization.PlatformBotUserId);

            var result = await resolver.ResolveMemberAsync(foreignOrganization.PlatformBotUserId, organization);

            Assert.NotNull(result);
            Assert.Equal("le abbot", result.DisplayName);
            Assert.False(result.IsGuest);
            Assert.True(result.User.IsAbbot);
            Assert.True(result.User.IsBot);
            Assert.Equal(foreignOrganization.PlatformBotUserId, result.User.PlatformUserId);
            Assert.Same(foreignOrganization, result.Organization);
            Assert.NotSame(env.TestData.ForeignAbbot, result);
            Assert.NotSame(env.TestData.ForeignAbbot.User, result.User);
        }

        [Fact]
        public async Task ReturnsRandomBotMember()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U99999",
                    TeamId = organization.PlatformId,
                    IsBot = true,
                    Profile = new UserProfile
                    {
                        RealName = "some-bot"
                    }
                });
            var resolver = env.Activate<SlackResolver>();
            Assert.NotNull(organization.PlatformBotUserId);

            var result = await resolver.ResolveMemberAsync("U99999", organization);

            Assert.NotNull(result);
            Assert.Null(result.User.NameIdentifier);
            Assert.True(result.User.IsBot);
            Assert.Equal("some-bot", result.DisplayName);
            Assert.Equal(organization.Id, result.OrganizationId);
        }

        [Fact]
        public async Task RetrievesExistingMemberAssociatedWithUserSlackTeamId()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var anotherOrganization = await env.CreateOrganizationAsync("T01212121");
            var user = env.TestData.User;
            user.SlackTeamId = anotherOrganization.PlatformId;
            user.Members.Add(new Member
            {
                Organization = anotherOrganization
            });

            await env.Db.SaveChangesAsync();
            Assert.Equal(2, user.Members.Count);
            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync(user.PlatformUserId, organization);

            Assert.NotNull(result);
            Assert.Equal(result.User.SlackTeamId, anotherOrganization.PlatformId);
            Assert.Equal(result.OrganizationId, anotherOrganization.Id);
            Assert.Equal(result.User.Id, env.TestData.User.Id);
        }

        [Fact]
        public async Task CreatesMemberInCurrentOrganization()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync("T0123456", apiToken: "xoxb-this-is-a-test-token");
            env.SlackApi.AddTeamInfo("xoxb-this-is-a-test-token",
                "T0123456",
                new TeamInfo
                {
                    Id = "T0123456",
                    Icon = new()
                    {
                        Image68 = "https://example.com/icon.png",
                    },
                    Name = "Test Team",
                });
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U0123456",
                    Name = "the-user",
                    TeamId = organization.PlatformId,
                    Profile = new UserProfile
                    {
                        DisplayName = "the-real-test-user",
                        RealName = "The Real Test User",
                        Email = "this@should.be.synced",
                    },
                    TimeZone = "America/New_York",
                });

            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync("U0123456", organization);

            Assert.NotNull(result);
            Assert.Equal(organization.Id, result.OrganizationId);
            Assert.Equal("U0123456", result.User.PlatformUserId);
            Assert.Equal("T0123456", result.User.SlackTeamId);
            Assert.Equal(string.Empty, result.Organization.EnterpriseGridId);
            Assert.Equal("the-real-test-user", result.User.DisplayName);
            Assert.Equal("The Real Test User", result.User.RealName);
            Assert.Equal("the-real-test-user", result.DisplayName);
            Assert.Equal("this@should.be.synced", result.User.Email);
            Assert.Equal("America/New_York", result.TimeZoneId);
        }

        [Fact]
        public async Task DoesNotSetEnterpriseIdWhenTeamInfoFails()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync("T0123456", apiToken: "xoxb-this-is-a-test-token");
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U0123456",
                    Name = "the-user",
                    TeamId = organization.PlatformId,
                    Profile = new UserProfile
                    {
                        DisplayName = "the-real-test-user",
                        RealName = "The Real Test User",
                        Email = "this@should.be.synced",
                    },
                    TimeZone = "America/New_York",
                });

            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync("U0123456", organization);

            Assert.NotNull(result);
            Assert.Null(result.Organization.EnterpriseGridId);
        }

        [Fact]
        public async Task CreatesMemberInCurrentEnterpriseOrganizationAndUpdatesOrganizationEnterpriseGridId()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync(
                platformId: "T0123456",
                apiToken: "xoxb-this-is-a-test-token");
            Assert.Null(organization.EnterpriseGridId);
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U0123456",
                    Name = "the-user",
                    TeamId = organization.PlatformId,
                    EnterpriseId = "E000001234",
                    Profile = new UserProfile
                    {
                        DisplayName = "the-real-test-user",
                        RealName = "The Real Test User",
                        Email = "this@should.be.synced",
                    },
                    TimeZone = "America/New_York",
                });

            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync("U0123456", organization);

            Assert.NotNull(result);
            Assert.Equal(organization.Id, result.OrganizationId);
            Assert.Equal("U0123456", result.User.PlatformUserId);
            Assert.Equal("T0123456", result.User.SlackTeamId);
            Assert.Equal("E000001234", result.Organization.EnterpriseGridId);
            Assert.Equal("the-real-test-user", result.User.DisplayName);
            Assert.Equal("The Real Test User", result.User.RealName);
            Assert.Equal("the-real-test-user", result.DisplayName);
            Assert.Equal("this@should.be.synced", result.User.Email);
            Assert.Equal("America/New_York", result.TimeZoneId);
        }

        [Fact]
        public async Task CreatesMemberInForeignEnterpriseOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U0123456",
                    Name = "the-user",
                    Profile = new UserProfile
                    {
                        DisplayName = "the-real-test-user",
                        RealName = "The Real Test User",
                        Email = "this@should.not.be.synced",
                    },
                    TimeZone = "America/New_York",
                    EnterpriseId = "E08675309"
                });

            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync("U0123456", organization);

            Assert.NotNull(result);
            Assert.NotEqual(organization.Id, result.OrganizationId);
            Assert.Equal("U0123456", result.User.PlatformUserId);
            Assert.Equal("E08675309", result.User.SlackTeamId);
            Assert.Equal("E08675309", result.Organization.EnterpriseGridId);
            Assert.Equal("the-real-test-user", result.User.DisplayName);
            Assert.Equal("The Real Test User", result.User.RealName);
            Assert.Equal("the-real-test-user", result.DisplayName);
            Assert.Equal("America/New_York", result.TimeZoneId);
            Assert.Null(result.User.Email);
        }

        [Fact]
        public async Task CreatesForeignMember()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync("T0123456", apiToken: "xoxb-this-is-a-test-token");
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U0123456",
                    Name = "the-user",
                    TeamId = "T8675309",
                    Profile = new UserProfile
                    {
                        DisplayName = "the-real-test-user",
                        RealName = "The real test user",
                        Email = "this@should.not.be.synced",
                    },
                    TimeZone = "America/New_York",
                });

            env.SlackApi.AddTeamInfo("xoxb-this-is-a-test-token",
                "T8675309",
                new TeamInfo
                {
                    Id = "T8675309",
                    Name = "Foreign Org",
                    Domain = "foreign",
                    Icon = new Icon
                    {
                        Image68 = "https://example.com/icon.png"
                    }
                });

            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync("U0123456", organization);

            Assert.NotNull(result);
            Assert.NotEqual(organization.Id, result.OrganizationId);
            Assert.Equal(PlatformType.Slack, result.Organization.PlatformType);
            Assert.Equal("T8675309", result.Organization.PlatformId);
            Assert.Equal("T8675309", result.User.SlackTeamId);
            Assert.Equal(string.Empty, result.Organization.EnterpriseGridId);
            Assert.Equal("Foreign Org", result.Organization.Name);
            Assert.Equal("U0123456", result.User.PlatformUserId);
            Assert.Equal("the-real-test-user", result.User.DisplayName);
            Assert.Equal("The real test user", result.User.RealName);
            Assert.Equal("the-real-test-user", result.DisplayName);
            Assert.Equal("America/New_York", result.TimeZoneId);
            Assert.Null(result.User.Email);
            Assert.Single(result.User.Members);
        }

        [Fact]
        public async Task ReturnsNullWhenUserDoesNotExistInApi()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync("T0123456", apiToken: "xoxb-this-is-a-test-token");
            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync("U0123456", organization);

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsNullWhenUserHasNoTeamIdNorAnEnterpriseId()
        {
            var env = TestEnvironment.CreateWithoutData();
            var organization = await env.CreateOrganizationAsync("T0123456", apiToken: "xoxb-this-is-a-test-token");
            env.SlackApi.AddUserInfoResponse("xoxb-this-is-a-test-token",
                new UserInfo
                {
                    Id = "U0123456",
                    Name = "the-user",
                    Profile = new UserProfile
                    {
                        RealName = "The real test user"
                    }
                });

            var resolver = env.Activate<SlackResolver>();

            var result = await resolver.ResolveMemberAsync("U0123456", organization);

            Assert.Null(result);
        }
    }

    public class TheResolveInstallEventFromOAuthResponseAsyncMethod
    {
        [Fact]
        public async Task ResolvesInstallEventFromOAuthResponseAndSlackAPIs()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ISlackApiClient>(out var apiClient)
                .Build();
            var resolver = env.Activate<SlackResolver>();

            var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("clientId:clientSecret"));
            apiClient.ExchangeOAuthCodeAsync($"Basic {authHeaderValue}", "https://app.ab.bot/slack/installed", "I am authentic")
                .Returns(new OAuthExchangeResponse()
                {
                    Ok = true,
                    AccessToken = "xoxb-access",
                    AppId = "A123",
                    BotUserId = "U123",
                    Scope = "mouthwash",
                    TokenType = "bot",
                    Team = new()
                    {
                        Id = "T123",
                        Name = "The Team"
                    },
                });

            apiClient.AuthTestAsync("xoxb-access")
                .Returns(
                    new ApiResponse<AuthTestResponse>(
                        new HttpResponseMessage(HttpStatusCode.OK),
                        content: new AuthTestResponse
                        {
                            Ok = true,
                            BotId = "B123",
                            Team = "The Team",
                            TeamId = "T123",
                            User = "abbot",
                            UserId = "U123"
                        },
                        new RefitSettings()));

            apiClient.GetTeamInfoAsync("xoxb-access", "T123")
                .Returns(new ApiResponse<TeamInfoResponse>(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Headers =
                            {
                                {"X-OAuth-Scopes", "mouthwash"}
                            },
                        },
                        new TeamInfoResponse()
                        {
                            Ok = true,
                            Body = new()
                            {
                                Id = "T123",
                                Domain = "domain",
                                Name = "The Team",
                                Icon = new()
                                {
                                    Image68 = "https://example.com/icon.png"
                                }
                            }
                        }, new RefitSettings()));

            apiClient.GetBotsInfoAsync("xoxb-access", "B123")
                .Returns(new BotInfoResponse()
                {
                    Ok = true,
                    Body = new()
                    {
                        Id = "id1",
                        Name = "The Bot",
                        AppId = "A123",
                        UserId = "U123",
                        Icons = new("img36.png", null, null)
                    }
                });

            apiClient.GetUserInfo("xoxb-access", "U123")
                .Returns(new UserInfoResponse()
                {
                    Ok = true,
                    Body = new()
                    {
                        Id = "id1",
                        Name = "abbot",
                    }
                });

            var installerPrincipal = new ClaimsPrincipal();
            var installEvent = await resolver.ResolveInstallEventFromOAuthResponseAsync("I am authentic",
                "clientId",
                "clientSecret",
                installerPrincipal);

            Assert.Equal("T123", installEvent.PlatformId);
            Assert.Equal(PlatformType.Slack, installEvent.PlatformType);
            Assert.Equal("B123", installEvent.BotId);
            Assert.Equal("abbot", installEvent.BotName);
            Assert.Equal("The Team", installEvent.Name);
            Assert.Equal("domain", installEvent.Slug);
            Assert.Equal("xoxb-access", installEvent.ApiToken?.Reveal());
            Assert.Equal("domain.slack.com", installEvent.Domain);
            Assert.Equal("mouthwash", installEvent.OAuthScopes);
            Assert.Equal("The Bot", installEvent.BotAppName);
            Assert.Equal("img36.png", installEvent.BotAvatar);
            Assert.Equal("U123", installEvent.BotUserId);
            Assert.Equal("A123", installEvent.AppId);
            Assert.Equal(installerPrincipal, installEvent.Installer);
        }
    }
}
