using Abbot.Common.TestHelpers;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Security;
using Serious.Abbot.Skills;
using Serious.TestHelpers;

public class ConversationsSkillTests
{
    // ReSharper disable once SuggestBaseTypeForParameter
    static bool IsRoomAssigned(Room room, RoomRole role, Member member)
    {
        return room.Assignments.Any(a => a.Role == role && a.MemberId == member.Id && a.RoomId == room.Id);
    }

    [Theory]
    [InlineData(PlanType.Free, $"Sorry, conversation tracking isn't included in your plan. Contact us at `{WebConstants.SupportEmail}` to discuss upgrading!")]
    [InlineData(PlanType.Team, $"Sorry, conversation tracking isn't included in your plan. Contact us at `{WebConstants.SupportEmail}` to discuss upgrading!")]
    [InlineData(PlanType.Business, "Conversation tracking is enabled in <#CTEST>.")]
    [InlineData(PlanType.FoundingCustomer, $"Sorry, conversation tracking isn't included in your plan. Contact us at `{WebConstants.SupportEmail}` to discuss upgrading!")]
    [InlineData(PlanType.Beta, "Conversation tracking is enabled in <#CTEST>.")]
    [InlineData(PlanType.Unlimited, "Conversation tracking is enabled in <#CTEST>.")]
    public async Task DoesNotDoAnythingUnlessPlanHasConversationTrackingEnabled(PlanType plan, string response)
    {
        var env = TestEnvironment.Create();
        env.TestData.Organization.PlanType = plan;
        await env.Db.SaveChangesAsync();

        var room = await env.CreateRoomAsync("CTEST", managedConversationsEnabled: true, persistent: true);
        var message = env.CreateFakeMessageContext("conversations", "status", room: room);
        var skill = env.Activate<ConversationsSkill>();

        await skill.OnMessageActivityAsync(message, default);

        Assert.Equal(response, message.SingleReply());
    }

    public class TheStatusCommand
    {
        [Fact]
        public async Task FailsIfRunFromOutsideAPersistentRoom()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: false);
            // Just making sure our test setup is correct
            Assert.Equal((false, false), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", "status", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal("Sorry, I cannot track conversations here.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.False(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task ReturnsDisabledIfSettingIsDisabled()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: false, persistent: true);
            Assert.False(room.ManagedConversationsEnabled);
            var message = env.CreateFakeMessageContext("conversations", "status", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking is disabled in <#{room.PlatformRoomId}>.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.False(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task ReturnsEnabledIfSettingIsEnabled()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true, persistent: true);
            Assert.True(room.ManagedConversationsEnabled);
            var message = env.CreateFakeMessageContext("conversations", "status", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking is enabled in <#{room.PlatformRoomId}>.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.True(room.ManagedConversationsEnabled);
        }
    }

    public class TheTrackCommand
    {
        [Fact]
        public async Task FailsIfRunFromOutsideAPersistentRoom()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: false);
            Assert.False(room.Persistent);
            var message = env.CreateFakeMessageContext("conversations", "track", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal("Sorry, I cannot track conversations here.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.False(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task UpdatesExistingRoomToEnableTracking()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: true);
            Assert.False(room.ManagedConversationsEnabled);
            var message = env.CreateFakeMessageContext("conversations", "track", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking has been enabled in <#{room.PlatformRoomId}>.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.True(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task NoOpsIfTrackingIsAlreadyEnabled()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true, persistent: true);
            Assert.Equal((true, true), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", "track", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking is already enabled in <#{room.PlatformRoomId}>.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.True(room.ManagedConversationsEnabled);
        }
    }

    public class TheUntrackCommand
    {
        [Fact]
        public async Task FailsIfRunFromOutsideAPersistentRoom()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: false);
            Assert.False(room.Persistent);
            var message = env.CreateFakeMessageContext("conversations", "untrack", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal("Sorry, I cannot track conversations here.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.False(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task NoOpsIfManagedConversationsAlreadyDisabled()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: false, persistent: true);
            Assert.Equal((true, false), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", "untrack", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking is already disabled in <#{room.PlatformRoomId}>.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.False(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task DisabledManagedConversationsIfEnabled()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true, persistent: true);
            Assert.Equal((true, true), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", "untrack", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking has been disabled in <#{room.PlatformRoomId}>. Existing conversation data is preserved.",
                message.SingleReply());
            await env.ReloadAsync(room);
            Assert.False(room.ManagedConversationsEnabled);
        }
    }

    public class TheForModifier
    {
        [Theory]
        [InlineData("for", "You didn’t specify a room name.", true)]
        [InlineData("for <#C999|fake-room>", "I couldn’t find the room 'fake-room' in my records. Maybe I’m not a member of the room?", false)]
        [InlineData("for <#C111|real-room>", "You didn’t specify a command!", true)]
        public async Task InvalidSyntaxTest(string arguments, string expectedError, bool includeUsage)
        {
            var env = TestEnvironment.Create();
            var botUserId = env.TestData.Organization.PlatformBotUserId;
            var room = await env.CreateRoomAsync("C111", "real-room");
            var message = env.CreateFakeMessageContext("conversations", arguments, room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal(expectedError, message.SentMessages[0]);

            if (includeUsage)
            {
                Assert.Equal(2, message.SentMessages.Count);

                // Not going to check the whole usage string.
                Assert.StartsWith($"`<@{botUserId}> conversations for", message.SentMessages[1]);
            }
            else
            {
                Assert.Equal(1, message.SentMessages.Count);
            }
        }

        [Fact]
        public async Task TrackSubcommandUpdatesExistingRoomToEnableTracking()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: true);
            Assert.Equal((true, false), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", $"for <#{room.PlatformRoomId}|{room.Name}> track", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking has been enabled in <#{room.PlatformRoomId}>.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.True(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task UntrackSubcommandUpdatesExistingRoomToDisableTracking()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            Assert.Equal((true, true), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", $"for <#{room.PlatformRoomId}|{room.Name}> untrack", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"Conversation tracking has been disabled in <#{room.PlatformRoomId}>. Existing conversation data is preserved.", message.SingleReply());
            await env.ReloadAsync(room);
            Assert.False(room.ManagedConversationsEnabled);
        }
    }

    public class TheRolesCommand
    {
        [Fact]
        public async Task ListsAvailableRoles()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: true);
            Assert.Equal((true, false), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", "roles", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal(@"Available Roles:
• first-responder - Individuals responsible for responding to new conversations.
• escalation-responder - Individuals notified of overdue conversation.",
                message.SingleReply());
        }
    }

    public class TheRoleListCommand
    {
        [Theory]
        [InlineData("first-responders", "first-responder")]
        [InlineData("first-responders list", "first-responder")]
        public async Task ShowsMessageIfNoAssignedUsers(string arguments, string role)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: true);
            var message = env.CreateFakeMessageContext("conversations", arguments, room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"No users are assigned to the '{role}' role.", message.SingleReply());
        }

        [Theory]
        [InlineData("first-responders")]
        [InlineData("first-responders list")]
        public async Task ListsUsersInRole(string arguments)
        {
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var organization = env.TestData.Organization;
            var admin = await env.CreateAdminMemberAsync();
            var room = await env.CreateRoomAsync(persistent: true);
            var firstResponder = env.TestData.Member;
            await env.Rooms.AssignMemberAsync(room, firstResponder, RoomRole.FirstResponder, admin);
            var message = env.CreateFakeMessageContext("conversations", arguments, room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($@"The following users are assigned to the 'first-responder' role:
• [{firstResponder.DisplayName}](https://{organization.Domain}/team/{firstResponder.User.PlatformUserId})".NormalizeLineEndings(),
                message.SingleReply().NormalizeLineEndings());
        }
    }

    public class TheRoleAssignCommand
    {
        [Theory]
        [InlineData("first-responder", RoomRole.FirstResponder)]
        [InlineData("escalation-responder", RoomRole.EscalationResponder)]
        public async Task AddsRoleAssignmentIfNoneExists(string roleName, RoomRole role)
        {
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync(persistent: true);
            Assert.Equal((true, false), (room.Persistent, room.ManagedConversationsEnabled));
            var message = env.CreateFakeMessageContext("conversations", $"{roleName} assign <@{member.User.PlatformUserId}>", room: room, mentions: new[] { member });
            var skill = env.Activate<ConversationsSkill>();
            Assert.False(IsRoomAssigned(room, role, member));

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"[{member.DisplayName}](https://{organization.Domain}/team/{member.User.PlatformUserId}) has been assigned the '{roleName}' role.", message.SingleReply());
            Assert.True(IsRoomAssigned(room, role, member));
        }

        [Theory]
        [InlineData("first-responder", RoomRole.FirstResponder)]
        [InlineData("escalation-responder", RoomRole.EscalationResponder)]
        public async Task ReportsIfRoleAssignmentExists(string roleName, RoomRole role)
        {
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var admin = await env.CreateAdminMemberAsync();
            var room = await env.CreateRoomAsync(persistent: true);
            await env.Rooms.AssignMemberAsync(room, member, role, admin);
            var message = env.CreateFakeMessageContext("conversations", $"{roleName} assign <@{member.User.PlatformUserId}>", room: room, mentions: new[] { member });
            var skill = env.Activate<ConversationsSkill>();
            Assert.True(IsRoomAssigned(room, role, member));

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"[{member.DisplayName}](https://{organization.Domain}/team/{member.User.PlatformUserId}) already has the '{roleName}' role.", message.SingleReply());
            Assert.True(IsRoomAssigned(room, role, member));
        }
    }

    public class TheRoleUnassignCommand
    {
        [Theory]
        [InlineData("first-responder", RoomRole.FirstResponder)]
        [InlineData("escalation-responder", RoomRole.EscalationResponder)]
        public async Task RemovesRoleAssignmentIfExists(string roleName, RoomRole role)
        {
            var env = TestEnvironment.Create();
            await env.Roles.AddUserToRoleAsync(env.TestData.Member, Roles.Agent, env.TestData.Abbot);
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var admin = await env.CreateAdminMemberAsync();
            var room = await env.CreateRoomAsync(persistent: true);
            await env.Rooms.AssignMemberAsync(room, member, role, admin);
            var message = env.CreateFakeMessageContext("conversations", $"{roleName} unassign <@{member.User.PlatformUserId}>", room: room, mentions: new[] { member });
            var skill = env.Activate<ConversationsSkill>();
            Assert.True(IsRoomAssigned(room, role, member));

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"[{member.DisplayName}](https://{organization.Domain}/team/{member.User.PlatformUserId}) has been removed from the '{roleName}' role.", message.SingleReply());
            Assert.False(IsRoomAssigned(room, role, member));
        }

        [Theory]
        [InlineData("first-responder", RoomRole.FirstResponder)]
        [InlineData("escalation-responder", RoomRole.EscalationResponder)]
        public async Task ReportsIfRoleAssignmentDoesNotExist(string roleName, RoomRole role)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync(persistent: true);
            var message = env.CreateFakeMessageContext("conversations", $"{roleName} unassign <@{member.User.PlatformUserId}>", room: room, mentions: new[] { member });
            var skill = env.Activate<ConversationsSkill>();
            Assert.False(IsRoomAssigned(room, role, member));

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal($"[{member.DisplayName}](https://{organization.Domain}/team/{member.User.PlatformUserId}) is not assigned the '{roleName}' role.", message.SingleReply());
            Assert.False(IsRoomAssigned(room, role, member));
        }
    }

    public class TheInfoCommand
    {
        [Fact]
        public async Task ShowsMessageIfMessageNotInConversation()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(persistent: true);
            var message = env.CreateFakeMessageContext("conversations", "info", room: room);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            Assert.Equal("This message is not part of a conversation.", message.SingleReply());
        }

        [Fact]
        public async Task ShowsInfoIfMessageInConversation()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = env.TestData.Member;
            var room = await env.CreateRoomAsync(persistent: true);
            var convo = new Conversation()
            {
                Id = 42,
                StartedBy = member,
                Title = "Convo Title",

                // We're going to exclude time values when testing anyway, it's too volatile without a clock abstraction (which is too heavy).
                Created = DateTime.UtcNow,
                LastMessagePostedOn = DateTime.UtcNow,
            };
            convo.Members.Add(new ConversationMember()
            {
                Conversation = convo,
                Member = member
            });
            var message = env.CreateFakeMessageContext("conversations", "info", room: room);
            message.ConversationMatch = new ConversationMatch(null, convo);
            var skill = env.Activate<ConversationsSkill>();

            await skill.OnMessageActivityAsync(message, default);

            // Post-process to strip out time values, because they're hard to test
            var fixedLines = message.SingleReply().ReplaceLineEndings().Split(Environment.NewLine)
                .Select(l => {
                    l = l.Trim();
                    if (l.StartsWith("Started"))
                    {
                        var byIndex = l.IndexOf("by", StringComparison.Ordinal);
                        return "Started " + l.Substring(byIndex);
                    }
                    if (l.StartsWith("Last message was"))
                    {
                        return "Last message was";
                    }

                    return l;
                }).ToArray();
            Assert.Equal(new[] {
                "Conversation ID: `42`",
                $"Started by [{member.DisplayName}](https://{organization.Domain}/team/{member.User.PlatformUserId})",
                "Title: `Convo Title`",
                "Last message was",
                "",
                "Participants:",
                "",
                $"• [{member.DisplayName}](https://{organization.Domain}/team/{member.User.PlatformUserId})",
            }, fixedLines);
        }
    }
}
