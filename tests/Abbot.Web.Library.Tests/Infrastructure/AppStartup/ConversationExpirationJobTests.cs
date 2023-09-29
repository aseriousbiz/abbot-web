using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Models;
using Serious.Abbot.Security;

public class ConversationExpirationJobTests
{
    #region TestData

    static DateTimeOffset TestDate(int day, int hour) => new(
        TestEnvironment.TestOClock.Year,
        TestEnvironment.TestOClock.Month + 1,
        day, hour, 0, 0, TimeSpan.Zero);

    public class TestData : CommonTestData
    {
        public Room Room { get; private set; } = null!;

        public Conversation Conversation { get; private set; } = null!;

        public Member AnotherFirstResponder { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await env.AddUserToRoleAsync(env.TestData.Member, Roles.Agent);
            Room = await env.CreateRoomAsync(managedConversationsEnabled: true, platformRoomId: "Croom");
            Room.TimeToRespond = new Threshold<TimeSpan>(
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(2));
            AnotherFirstResponder = await env.CreateMemberInAgentRoleAsync(platformUserId: "Uother_fr");
            await env.AssignMemberAsync(Room, env.TestData.Member, RoomRole.FirstResponder);
            await env.AssignMemberAsync(Room, AnotherFirstResponder, RoomRole.FirstResponder);

            Conversation = await env.CreateConversationAsync(Room, "CONVO-WARN-0", TestDate(2, 11), env.TestData.ForeignMember,
                properties: new() { LastSupporteeMessageId = "1112.0000" },
                initialState: ConversationState.New); // In warning breach.
        }
    }

    #endregion TestData

    [UsesVerify]
    public class TheEnqueueNotificationsForOverdueConversationsAsyncMethod
    {
        static async Task Verify(TestEnvironmentWithData<TestData> env)
        {
            Assert.True(env.ConfiguredForSnapshot);
            var pending = await env.Db.PendingMemberNotifications
                .Select(p => new { p.ConversationId, p.MemberId })
                .ToListAsync();
            await env.ReloadAsync(env.TestData.Conversation);
            await Verifier.Verify(new {
                target = new {
                    env.BusTestHarness,
                    env.TestData.Conversation,
                    env.TestData.Conversation.Events,
                    env.SignalHandler.RaisedSignals,
                    Pending = pending,
                },
                logs = env.GetAllLogs(),
            });
        }

        static TestEnvironmentWithData<TestData> CreateTestEnvironment()
        {
            var env = TestEnvironmentBuilder.Create<TestData>()
                .Build(snapshot: true);

            env.Clock.TravelTo(TestEnvironment.TestOClock);
            return env;
        }

        [Fact]
        public async Task CreatesPendingNotificationForEveryFirstResponderForConversationsInWarningThresholdAndRaisesSystemSignal()
        {
            var now = TestDate(3, 12).DateTime;
            var env = CreateTestEnvironment();
            // Since room has first responder, we'll ignore the default one.
            await env.CreateMemberInAgentRoleAsync(isDefaultResponder: true, platformUserId: "UDEFAULT");
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);

            Assert.Equal(now, env.TestData.Conversation.TimeToRespondWarningNotificationSent);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForAssigneeForConversationsInWarningThresholdInsteadOfFirstResponders()
        {
            var now = TestDate(3, 12).DateTime;
            var env = CreateTestEnvironment();
            var conversation = env.TestData.Conversation;
            var assignee = await env.CreateMemberInAgentRoleAsync(platformUserId: "Uassignee");
            conversation.Assignees.Add(assignee);
            await env.Db.SaveChangesAsync();
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);

            Assert.Equal(now, env.TestData.Conversation.TimeToRespondWarningNotificationSent);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForEveryDefaultFirstResponderForConversationsInWarningThresholdInRoomWithNoFirstResponders()
        {
            var now = TestDate(3, 12).DateTime;
            var env = CreateTestEnvironment();
            env.TestData.Room.Assignments.Clear();
            await env.AssignDefaultFirstResponderAsync(env.TestData.Member);
            await env.AssignDefaultFirstResponderAsync(env.TestData.AnotherFirstResponder);
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);

            Assert.Equal(now, env.TestData.Conversation.TimeToRespondWarningNotificationSent);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForEveryFirstResponderForConversationsInWarningThresholdOfOrganizationalDefaults()
        {
            var now = TestDate(3, 12).DateTime;
            var env = CreateTestEnvironment();
            var room = env.TestData.Room;
            var organization = env.TestData.Organization;
            // Replace the room settings with organizational defaults.
            organization.DefaultTimeToRespond = new Threshold<TimeSpan>(room.TimeToRespond.Warning, room.TimeToRespond.Deadline);
            room.TimeToRespond = new Threshold<TimeSpan>(null, null);
            await env.Db.SaveChangesAsync();
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);

            Assert.Equal(now, env.TestData.Conversation.TimeToRespondWarningNotificationSent);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForEveryFirstResponderForOverdueConversationAndRaisesSystemSignal()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            // Since room has first responder, we'll ignore the default one.
            await env.CreateMemberInAgentRoleAsync(isDefaultResponder: true, platformUserId: "Uother_default");
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForAssigneeAndNotFirstRespondersForOverdueConversation()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            var conversation = env.TestData.Conversation;
            var assignee = env.TestData.Member;
            conversation.Assignees.Add(assignee);
            await env.Db.SaveChangesAsync();
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task RaisesSystemSignalEvenWhenNoFirstResponders()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            env.TestData.Room.Assignments.Clear();
            await env.Db.SaveChangesAsync();
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForEscalationRespondersAndFirstRespondersForOverdueConversation()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            var escalationResponder = await env.CreateMemberInAgentRoleAsync(platformUserId: "Uescalator");
            await env.AssignMemberAsync(env.TestData.Room, escalationResponder, RoomRole.EscalationResponder);
            await env.AssignMemberAsync(env.TestData.Room, escalationResponder, RoomRole.FirstResponder);
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task OnlyCreatesPendingNotificationForEscalationRespondersIfAssigneeIsAlsoEscalationResponderForOverdueConversation()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            var escalationResponder = await env.CreateMemberInAgentRoleAsync(platformUserId: "Uescalator");
            var conversation = env.TestData.Conversation;
            conversation.Assignees.Add(escalationResponder);
            await env.Db.SaveChangesAsync();
            await env.AssignMemberAsync(env.TestData.Room, escalationResponder, RoomRole.EscalationResponder);
            await env.AssignMemberAsync(env.TestData.Room, escalationResponder, RoomRole.FirstResponder);
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForEscalationRespondersForOverdueConversationEvenWhenThereAreNoFirstResponders()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            env.TestData.Room.Assignments.Clear();
            await env.Db.SaveChangesAsync();
            var escalationResponder = await env.CreateMemberInAgentRoleAsync(platformUserId: "Uescalator");
            await env.AssignMemberAsync(env.TestData.Room, escalationResponder, RoomRole.EscalationResponder);
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForDefaultEscalationRespondersForOverdueConversation()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            var escalationResponder = await env.CreateMemberInAgentRoleAsync(platformUserId: "Uescalator");
            await env.AssignDefaultEscalationResponderAsync(escalationResponder);
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task CreatesPendingNotificationForEveryDefaultFirstResponderForOverdueConversationWithRoomWithNoFirstResponder()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            var firstResponder = env.TestData.Member;
            var otherFirstResponder = env.TestData.AnotherFirstResponder;
            env.TestData.Room.Assignments.Clear();
            await env.AssignDefaultFirstResponderAsync(firstResponder);
            await env.AssignDefaultFirstResponderAsync(otherFirstResponder);
            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task DoesNotCreatesPendingNotificationIfRoomHasNoFirstResponders()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            env.TestData.Room.Assignments.Clear();
            await env.Db.SaveChangesAsync();

            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task DoesNotCreatesPendingNotificationIfRoomHasConversationTrackingDisabled()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            env.TestData.Room.ManagedConversationsEnabled = false;
            await env.Db.SaveChangesAsync();

            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task DoesNotCreatesPendingNotificationIfOrganizationDoesNotHaveConversationTrackingEnabled()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            env.TestData.Organization.PlanType = PlanType.Free;
            await env.Db.SaveChangesAsync();

            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }

        [Fact]
        public async Task DoesNotCreatesPendingNotificationIfNotifyOnlyOnNewSetAndConversationNotNew()
        {
            var now = TestDate(4, 12).DateTime;
            var env = CreateTestEnvironment();
            var organization = env.TestData.Organization;
            env.TestData.Conversation.State = ConversationState.NeedsResponse;
            organization.Settings = organization.Settings with
            {
                NotifyOnNewConversationsOnly = true,
            };
            await env.Db.SaveChangesAsync();

            var notifier = env.Activate<ConversationExpirationJob>();

            await notifier.EnqueueNotificationsForOverdueConversationsAsync(now);
            await Verify(env);
        }
    }
}
