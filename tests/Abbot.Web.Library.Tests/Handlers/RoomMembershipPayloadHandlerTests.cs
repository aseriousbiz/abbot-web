using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Security;
using Serious.Abbot.Signals;
using Serious.Abbot.Telemetry;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;
using Serious.Slack.Payloads;
using Serious.TestHelpers.CultureAware;
using static Serious.Abbot.PayloadHandlers.RoomMembershipPayloadHandler;

public class RoomMembershipPayloadHandlerTests
{
    static SettingsTask Verify(TestEnvironmentWithData env, Room room) =>
        Verifier.Verify(BuildTarget(env, room));

    static async Task<object> BuildTarget(TestEnvironmentWithData env, Room room)
    {
        Assert.True(env.ConfiguredForSnapshot);
        await env.ReloadAsync(room);
        return new {
            // Preserve old snapshot format for easier reviewing.
            target = new {
                Room = room,
                room.Assignments,
                room.TimeToRespond,
                env.Responder.OpenModals,
                env.Responder.SentMessages,
                env.AnalyticsClient,
                AuditEvents = await env.GetAllActivityAsync(room.Organization),
            },
            logs = env.GetAllLogs(),
        };
    }

    [UsesVerify]
    public class TheOnPlatformEventAsyncMethod
    {
        [Theory]
        [InlineData(false, MembershipChangeType.Added, true, "Abbot invited to room")]
        [InlineData(true, MembershipChangeType.Added, true, "Abbot invited to room")]
        [InlineData(false, MembershipChangeType.Removed, false, "Abbot removed from room")]
        [InlineData(true, MembershipChangeType.Removed, false, "Abbot removed from room")]
        public async Task WithRoomMembershipUpdateForBotItUpdatesTheBotIsMemberProperty(
            bool startBotIsMember,
            MembershipChangeType changeType,
            bool expectedBotIsMember,
            string expectedEventName)
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var platformMessageTranslator)
                .Build();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync(name: "test-room");
            room.BotIsMember = startBotIsMember;
            await env.Db.SaveChangesAsync();
            var payload = new RoomMembershipEventPayload(
                changeType,
                room.PlatformRoomId,
                organization.PlatformBotUserId!,
                InviterPlatformUserId: env.TestData.User.PlatformUserId);

            var roomEvent = env.CreateFakePlatformEvent(payload, env.TestData.Abbot);
            platformMessageTranslator.TranslateEventAsync(Args.TurnContext).Returns(roomEvent);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnPlatformEventAsync(roomEvent);

            await env.ReloadAsync(room);
            Assert.Equal(expectedBotIsMember, room.BotIsMember);
            env.AnalyticsClient.AssertTracked(
                expectedEventName,
                AnalyticsFeature.Slack,
                env.TestData.Member,
                new {
                    inviter_can_manage = false,
                    room = $"{room.Id}",
                    room_is_shared = room.Shared.ToString() ?? "null",
                    plan_supports_conversation_tracking = true
                }
            );

            if (changeType is MembershipChangeType.Added)
            {
                Assert.Contains(env.SignalHandler.RaisedSignals,
                    raised => raised.Name == SystemSignal.AbbotAddedToRoom.Name);
            }
            else
            {
                Assert.DoesNotContain(env.SignalHandler.RaisedSignals,
                    raised => raised.Name == SystemSignal.AbbotAddedToRoom.Name);
            }
        }

        [Theory]
        [InlineData(false, MembershipChangeType.Added, false)]
        [InlineData(true, MembershipChangeType.Added, false)] // Undelete if reinvited
        [InlineData(false, MembershipChangeType.Removed, false)]
        [InlineData(true, MembershipChangeType.Removed, true)] // Stay deleted
        public async Task WithRoomMembershipUpdateForBotItUpdatesTheDeletedProperty(
            bool startDeleted,
            MembershipChangeType changeType,
            bool expectedDeleted)
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var platformMessageTranslator)
                .Build();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync(name: "test-room");
            room.Deleted = startDeleted;
            await env.Db.SaveChangesAsync();
            var payload = new RoomMembershipEventPayload(
                changeType,
                room.PlatformRoomId,
                organization.PlatformBotUserId!,
                InviterPlatformUserId: env.TestData.User.PlatformUserId);

            var roomEvent = env.CreateFakePlatformEvent(payload, env.TestData.Abbot);
            platformMessageTranslator.TranslateEventAsync(Args.TurnContext).Returns(roomEvent);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnPlatformEventAsync(roomEvent);

            await env.ReloadAsync(room);
            Assert.Equal(expectedDeleted, room.Deleted);
        }

        [Theory]
        [InlineData(false, MembershipChangeType.Added)]
        [InlineData(true, MembershipChangeType.Added)]
        [InlineData(false, MembershipChangeType.Removed)]
        [InlineData(true, MembershipChangeType.Removed)]
        public async Task WithRoomMembershipUpdateForDisabledBotItUpdatesNothing(
            bool startBotIsMember,
            MembershipChangeType changeType)
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<ITurnContextTranslator>(out var platformMessageTranslator)
                .Build();
            var room = await env.CreateRoomAsync(name: "test-room");
            room.BotIsMember = startBotIsMember;
            await env.Db.SaveChangesAsync();

            var wrongAbbot = env.TestData.ForeignAbbot;
            var payload = new RoomMembershipEventPayload(
                changeType,
                room.PlatformRoomId,
                wrongAbbot.User.PlatformUserId, // Abbot, but not this Org's Abbot
                InviterPlatformUserId: env.TestData.User.PlatformUserId);

            var roomEvent = env.CreateFakePlatformEvent(payload, wrongAbbot);
            platformMessageTranslator.TranslateEventAsync(Args.TurnContext).Returns(roomEvent);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnPlatformEventAsync(roomEvent);

            await env.ReloadAsync(room);
            Assert.Equal(startBotIsMember, room.BotIsMember);
            Assert.Empty(env.AnalyticsClient.Tracked);
            Assert.Empty(env.Responder.OpenModals);
            Assert.Empty(env.Responder.SentMessages);
        }

        [Theory]
        [InlineData(MembershipChangeType.Added, "UHOME", "CNOTPERSISTENT", true, "Yo dude", null)]
        [InlineData(MembershipChangeType.Added, "UHOME", "CNOTTRACKING", true, "Yo dude", null)]
        [InlineData(MembershipChangeType.Added, "UHOME", "CTRACKING", true, "Yo dude", null)]
        [InlineData(MembershipChangeType.Added, "UFOREIGN", "CNOTPERSISTENT", true, "Yo dude", null)]
        [InlineData(MembershipChangeType.Added, "UFOREIGN", "CNOTTRACKING", true, "Yo dude", null)]
        [InlineData(MembershipChangeType.Added, "UFOREIGN", "CTRACKING", false, "Yo dude", null)]
        [InlineData(MembershipChangeType.Added,
            "UFOREIGN",
            "CTRACKING",
            true,
            null,
            RoomSettings.DefaultUserWelcomeMessage)]
        [InlineData(MembershipChangeType.Added, "UFOREIGN", "CTRACKING", true, "Yo dude", "Yo dude")]
        public async Task WithRoomMembershipUpdateSendsEphemeralWelcomeMessageToForeignUser(
            MembershipChangeType changeType,
            string userId,
            string roomId,
            bool? organizationMessageEnabled,
            string? organizationMessage,
            string? expectedMessage)
        {
            var env = TestEnvironment.Create();

            env.TestData.Organization.DefaultRoomSettings = new()
            {
                WelcomeNewUsers = organizationMessageEnabled,
                UserWelcomeMessage = organizationMessage
            };

            await env.Db.SaveChangesAsync();

            await env.CreateMemberAsync("UHOME", "test-home-user");
            await env.CreateMemberAsync("UFOREIGN", "test-foreign-user", org: env.TestData.ForeignOrganization);
            var from = await env.Db.Members.SingleAsync(m => m.User.PlatformUserId == userId);
            await env.CreateRoomAsync("CNOTPERSISTENT", "not-persistent", persistent: false);
            await env.CreateRoomAsync("CNOTTRACKING",
                "not-tracking",
                persistent: true,
                managedConversationsEnabled: false);

            await env.CreateRoomAsync("CTRACKING", "not-tracking", persistent: true, managedConversationsEnabled: true);
            await env.CreateRoomAsync(name: "test-room");
            var roomEvent =
                env.CreateFakePlatformEvent(new RoomMembershipEventPayload(changeType, roomId, userId), from);

            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnPlatformEventAsync(roomEvent);
            var activity = env.Responder.SentMessages.SingleOrDefault();
            Assert.Equal(expectedMessage, activity?.Text);
            if (activity is not null)
            {
                var richActivity = Assert.IsType<RichActivity>(activity);
                Assert.Equal(userId, richActivity.EphemeralUser);
            }
        }

        [Theory]
        [InlineData(Roles.Administrator, RoomFlags.Default)]
        [InlineData(Roles.Administrator, RoomFlags.Shared)]
        [InlineData(Roles.Agent, RoomFlags.Default)]
        [InlineData(Roles.Agent, RoomFlags.Shared)]
        public async Task WhenAddingAbbotEnablesTrackingAndShowsMessageWithButtonToEditConversationTracking(string role, RoomFlags roomFlags)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var org = env.TestData.Organization;
            await env.AddUserToRoleAsync(env.TestData.Member, role);
            var room = await env.CreateRoomAsync(RoomFlags.BotIsNotMember | roomFlags);
            Assert.False(room.ManagedConversationsEnabled);

            var payload = new RoomMembershipEventPayload(
                MembershipChangeType.Added,
                room.PlatformRoomId,
                org.PlatformBotUserId!,
                env.TestData.User.PlatformUserId);

            var platformEvent = env.CreateFakePlatformEvent(payload, from: env.TestData.Abbot);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            await Verify(env, room).UseParameters(role, roomFlags);
        }

        [Theory]
        [InlineData(null, RoomFlags.Default)]
        [InlineData(null, RoomFlags.Shared)]
        [InlineData(Roles.Administrator, RoomFlags.Default)]
        [InlineData(Roles.Administrator, RoomFlags.Shared)]
        [InlineData(Roles.Agent, RoomFlags.Default)]
        [InlineData(Roles.Agent, RoomFlags.Shared)]
        public async Task WhenAddingAbbotButPlanDoesNotAllowConversationsEnablesTrackingAnywayAndShowsMessageWithButtonToUpgrade(string? role, RoomFlags roomFlags)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var org = env.TestData.Organization;
            env.TestData.Organization.PlanType = PlanType.Free;
            env.TestData.Organization.Trial = new TrialPlan(PlanType.Free, DateTime.UtcNow.AddMinutes(-1)); // Expired
            if (role is not null)
            {
                await env.AddUserToRoleAsync(env.TestData.Member, role);
            }
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync(roomFlags);
            Assert.False(room.ManagedConversationsEnabled);

            var payload = new RoomMembershipEventPayload(
                MembershipChangeType.Added,
                room.PlatformRoomId,
                org.PlatformBotUserId!,
                env.TestData.User.PlatformUserId);

            var platformEvent = env.CreateFakePlatformEvent(payload, from: env.TestData.Abbot);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            await Verify(env, room).UseParameters(role, roomFlags);
        }

        [Theory]
        [InlineData(RoomFlags.Default)]
        [InlineData(RoomFlags.Shared)]
        [InlineData(RoomFlags.IsCommunity)]
        public async Task WhenAddingAbbotButCannotManageConversationsEnablesTrackingAnywayAndShowsMessageWithLinkToHome(RoomFlags roomFlags)
        {
            var env = TestEnvironment.Create(snapshot: true);
            var org = env.TestData.Organization;
            var room = await env.CreateRoomAsync(roomFlags);
            Assert.False(room.ManagedConversationsEnabled);

            var payload = new RoomMembershipEventPayload(
                MembershipChangeType.Added,
                room.PlatformRoomId,
                org.PlatformBotUserId!,
                env.TestData.User.PlatformUserId);

            var platformEvent = env.CreateFakePlatformEvent(payload, from: env.TestData.Abbot);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnPlatformEventAsync(platformEvent);

            await Verify(env, room).UseParameters(roomFlags);
        }
    }

    [UsesVerify]
    public class TheOnMessageInteractionAsyncMethod
    {
        [Theory]
        [InlineData(Roles.Administrator, RoomFlags.Default)]
        [InlineData(Roles.Administrator, RoomFlags.Shared)]
        [InlineData(Roles.Administrator, RoomFlags.IsCommunity)]
        [InlineData(Roles.Administrator, RoomFlags.ManagedConversationsEnabled)]
        [InlineData(Roles.Administrator, RoomFlags.ManagedConversationsEnabled | RoomFlags.Shared)]
        [InlineData(Roles.Administrator, RoomFlags.ManagedConversationsEnabled | RoomFlags.IsCommunity)]
        [InlineData(Roles.Agent, RoomFlags.Default)]
        [InlineData(Roles.Agent, RoomFlags.Shared)]
        [InlineData(Roles.Agent, RoomFlags.IsCommunity)]
        [InlineData(Roles.Agent, RoomFlags.ManagedConversationsEnabled)]
        [InlineData(Roles.Agent, RoomFlags.ManagedConversationsEnabled | RoomFlags.Shared)]
        [InlineData(Roles.Agent, RoomFlags.ManagedConversationsEnabled | RoomFlags.IsCommunity)]
        public async Task WhenClickingEditConversationTrackingShowsModal(
            string role,
            RoomFlags roomFlags)
        {
            var env = TestEnvironment.Create(snapshot: true);
            await env.AddUserToRoleAsync(env.TestData.Member, role);
            var room = await env.CreateRoomAsync(roomFlags);
            var initialManagedConversationsEnabled = room.ManagedConversationsEnabled;

            var callbackInfo = new InteractionCallbackInfo(nameof(RoomMembershipPayloadHandler));
            var payload = new MessageBlockActionsPayload
            {
                Actions = new[]
                {
                    new ButtonElement { ActionId = ActionIds.EditConversationTrackingButton }
                },
                TriggerId = "the-trigger-id",
                ResponseUrl = new Uri("https://example.com/callback"),
                Container = new MessageContainer("timestamp", false, "channelid"),
            };
            var platformMessage = env.CreatePlatformMessage(
                room,
                callbackInfo,
                messageBlockActionsPayload: payload);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room).UseParameters(role, roomFlags);
            Assert.Equal(initialManagedConversationsEnabled, room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task WhenClickingTakeMeToAbbotModalNotShown()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync();
            Assert.False(room.ManagedConversationsEnabled);

            var callbackInfo = new InteractionCallbackInfo(nameof(RoomMembershipPayloadHandler));
            var payload = new MessageBlockActionsPayload
            {
                Actions = new[]
                {
                    new ButtonElement(),
                },
                TriggerId = "the-trigger-id",
                ResponseUrl = new Uri("https://example.com/callback"),
                Container = new MessageContainer("timestamp", false, "channelid"),
            };
            var platformMessage = env.CreatePlatformMessage(
                room,
                callbackInfo,
                messageBlockActionsPayload: payload);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room);
        }

        [Fact]
        public async Task WhenClickingEditConversationTrackingButOnFreePlanRespondsThatCustomerMustUpgradePlan()
        {
            var env = TestEnvironment.Create(snapshot: true);
            env.TestData.Organization.PlanType = PlanType.Free;
            env.TestData.Organization.Trial = new TrialPlan(PlanType.Business, DateTime.UtcNow.AddMinutes(-1)); // Expired
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            Assert.False(room.ManagedConversationsEnabled);

            var callbackInfo = new InteractionCallbackInfo(nameof(RoomMembershipPayloadHandler));
            var payload = new MessageBlockActionsPayload
            {
                Actions = new[]
                {
                    new ButtonElement { ActionId = ActionIds.EditConversationTrackingButton }
                },
                TriggerId = "the-trigger-id",
                ResponseUrl = new Uri("https://example.com/callback"),
                Container = new MessageContainer("timestamp", false, "channelid"),
            };
            var platformMessage = env.CreatePlatformMessage(room,
                callbackInfo,

                messageBlockActionsPayload: payload);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room);
            Assert.False(room.ManagedConversationsEnabled);
        }

        [Fact]
        public async Task WhenClickingEditConversationTrackingButCannotManageConversationsShowsMessage()
        {
            var env = TestEnvironment.Create(snapshot: true);
            var room = await env.CreateRoomAsync();
            Assert.False(room.ManagedConversationsEnabled);

            var callbackInfo = new InteractionCallbackInfo(nameof(RoomMembershipPayloadHandler));
            var payload = new MessageBlockActionsPayload
            {
                Actions = new[]
                {
                    new ButtonElement { ActionId = ActionIds.EditConversationTrackingButton }
                },
                TriggerId = "the-trigger-id",
                ResponseUrl = new Uri("https://example.com/callback"),
                Container = new MessageContainer("timestamp", false, "channelid"),
            };
            var platformMessage = env.CreatePlatformMessage(room,
                callbackInfo,
                                messageBlockActionsPayload: payload);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnMessageInteractionAsync(platformMessage);

            await Verify(env, room);
            Assert.False(room.ManagedConversationsEnabled);
        }
    }

    [UsesVerify]
    public class TheOnSubmissionAsyncMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData(Roles.Administrator)]
        [InlineData(Roles.Agent)]
        [UseCulture("en-US")]
        public async Task SavesFirstRespondersAndResponseTimesAndRepliesToRoomWithResponse(string? role)
        {
            var env = TestEnvironment.Create(snapshot: true);
            await env.AddUserToRoleAsync(env.TestData.Member, role);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            AssertInitialRoomState(room);
            var jerry = env.TestData.User;
            var elaine = (await env.CreateMemberAsync()).User;
            var george = (await env.CreateMemberAsync()).User;
            var selectedIds = new[] { jerry.PlatformUserId, elaine.PlatformUserId, george.PlatformUserId };
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [nameof(SubmissionState.FirstResponders)] = new()
                        {
                            [nameof(SubmissionState.FirstResponders)] = new UsersMultiSelectMenu
                            {
                                SelectedValues = selectedIds
                            }
                        },
                        [nameof(SubmissionState.TargetResponseTime)] = new()
                        {
                            [nameof(SubmissionState.TargetResponseTime)] = new StaticSelectMenu
                            {
                                SelectedOption = new Option("1 hour", TimeSpan.FromHours(1).ToString())
                            }
                        },
                        [nameof(SubmissionState.DeadlineResponseTime)] = new()
                        {
                            [nameof(SubmissionState.DeadlineResponseTime)] = new StaticSelectMenu
                            {
                                SelectedOption = new Option("2 hours", TimeSpan.FromHours(2).ToString())
                            }
                        },
                    }),
                    PrivateMetadata = $"{room.PlatformRoomId}|https://example.com/callback"
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewSubmissionPayload>(payload);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnSubmissionAsync(new ViewContext<IViewSubmissionPayload>(platformEvent, handler));

            await Verify(env, room).UseParameters(role);

            // Shouldn't have changed without permissions
            if (role is null)
            {
                AssertInitialRoomState(room);
            }

            void AssertInitialRoomState(Room room)
            {
                Assert.Empty(room.GetFirstResponders());
                Assert.Null(room.TimeToRespond.Warning);
                Assert.Null(room.TimeToRespond.Deadline);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(Roles.Administrator)]
        [InlineData(Roles.Agent)]
        [UseCulture("en-US")]
        public async Task KeepsExistingFirstRespondersAndSavesTimeToRespond(string? role)
        {
            var env = TestEnvironment.Create(snapshot: true);
            await env.AddUserToRoleAsync(env.TestData.Member, role);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var jerry = env.TestData.User;
            var elaine = (await env.CreateMemberAsync()).User;
            var george = (await env.CreateMemberAsync()).User;
            var selectedIds = new[] { jerry.PlatformUserId, elaine.PlatformUserId, george.PlatformUserId };
            await env.SetRoomAssignmentsAsync(room, selectedIds, RoomRole.FirstResponder);
            await env.Db.SaveChangesAsync();
            AssertInitialRoomState(room);
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [nameof(SubmissionState.FirstResponders)] = new()
                        {
                            [nameof(SubmissionState.FirstResponders)] = new UsersMultiSelectMenu
                            {
                                SelectedValues = selectedIds
                            }
                        },
                        [nameof(SubmissionState.TargetResponseTime)] = new()
                        {
                            [nameof(SubmissionState.TargetResponseTime)] = new StaticSelectMenu
                            {
                                SelectedOption = new Option("1 hour", TimeSpan.FromHours(1).ToString())
                            }
                        },
                        [nameof(SubmissionState.DeadlineResponseTime)] = new()
                        {
                            [nameof(SubmissionState.DeadlineResponseTime)] = new StaticSelectMenu
                            {
                                SelectedOption = new Option("2 hours", TimeSpan.FromHours(2).ToString())
                            }
                        },
                    }),
                    PrivateMetadata = $"{room.PlatformRoomId}|https://example.com/callback"
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewSubmissionPayload>(payload);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnSubmissionAsync(new ViewContext<IViewSubmissionPayload>(platformEvent, handler));

            await Verify(env, room).UseParameters(role);

            // Shouldn't have changed without permissions
            if (role is null)
            {
                AssertInitialRoomState(room);
            }

            void AssertInitialRoomState(Room room)
            {
                Assert.Equal(selectedIds, room.GetFirstResponders().Select(m => m.User.PlatformUserId));
                Assert.Null(room.TimeToRespond.Warning);
                Assert.Null(room.TimeToRespond.Deadline);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData(Roles.Administrator)]
        [InlineData(Roles.Agent)]
        [UseCulture("en-US")]
        public async Task SavesFirstRespondersAndLeavesResponseTimeAloneIfNotSet(string? role)
        {
            var env = TestEnvironment.Create(snapshot: true);
            await env.AddUserToRoleAsync(env.TestData.Member, role);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var jerry = env.TestData.User;
            var elaine = (await env.CreateMemberAsync()).User;
            var george = (await env.CreateMemberAsync()).User;
            var selectedIds = new[] { jerry.PlatformUserId, elaine.PlatformUserId, george.PlatformUserId };
            room.TimeToRespond = new Threshold<TimeSpan>(TimeSpan.FromMinutes(42), TimeSpan.FromMinutes(47));
            await env.Db.SaveChangesAsync();
            AssertInitialRoomState(room);
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [nameof(SubmissionState.FirstResponders)] = new()
                        {
                            [nameof(SubmissionState.FirstResponders)] = new UsersMultiSelectMenu
                            {
                                SelectedValues = selectedIds
                            }
                        }
                    }),
                    PrivateMetadata = $"{room.PlatformRoomId}|https://example.com/callback"
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewSubmissionPayload>(payload);
            var handler = env.Activate<RoomMembershipPayloadHandler>();

            await handler.OnSubmissionAsync(new ViewContext<IViewSubmissionPayload>(platformEvent, handler));

            await Verify(env, room).UseParameters(role);

            // Shouldn't have changed without permissions
            if (role is null)
            {
                AssertInitialRoomState(room);
            }

            void AssertInitialRoomState(Room room)
            {
                Assert.Empty(room.GetFirstResponders());
                Assert.Equal(TimeSpan.FromMinutes(42), room.TimeToRespond.Warning);
                Assert.Equal(TimeSpan.FromMinutes(47), room.TimeToRespond.Deadline);
            }
        }
    }
}
