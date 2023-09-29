using Abbot.Common.TestHelpers;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Telemetry;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Eventing.Consumers;

public class AttachConversationToHubConsumerTests
{
    static SettingsTask Verify(TestEnvironmentWithData env, Conversation convo)
    {
        return VerifierExt.Verify(async () => new {
            // Match the old snapshot format for simplicity
            target = new {
                Conversation = convo,
                env.SlackApi.PostedMessages,
                AuditEvents = await env.GetAllActivityAsync(),
            },
            logs = env.GetAllLogs()
        });
    }

    [UsesVerify]
    public class WithANewConversationMessage
    {
        [Theory]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, null)]
        public async Task NoOpsIfDataIsIncomplete(bool validOrganizationId, bool validConvoId, bool? validRoomHubId)
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AttachConversationToHubConsumer>()
                .Build(snapshot: true);

            var sourceRoom = await env.CreateRoomAsync();
            var hub = await env.CreateHubAsync();
            var convo = await env.CreateConversationAsync(sourceRoom);

            var message = new NewConversation(
                validConvoId ? convo : new Id<Conversation>(99),
                validOrganizationId ? env.TestData.Organization : new Id<Organization>(99),
                validRoomHubId switch
                {
                    true => hub,
                    false => new Id<Hub>(99),
                    _ => null
                },
                new Uri("https://example.com/"));

            await env.PublishAndWaitForConsumptionAsync(message);

            await env.ReloadAsync(convo);
            await Verify(env, convo)
                .UseParameters(
                    validOrganizationId ? "ValidOrg" : "InvalidOrg",
                    validConvoId ? "ValidConvo" : "InvalidConvo",
                    validRoomHubId switch
                    {
                        true => "ValidRoomHub",
                        false => "InvalidRoomHub",
                        _ => "NoRoomHub"
                    });
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(null, false)]
        [InlineData(null, true)]
        [InlineData(false, null)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, null)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task AttachesConversationToRoomHubIdIgnoringRoomAndDefaultHub(bool? defaultHubMatches, bool? roomHubMatches)
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AttachConversationToHubConsumer>()
                .Build(snapshot: true);

            var actor = env.TestData.Member;
            var sourceRoom = await env.CreateRoomAsync();
            var hub = await env.CreateHubAsync();
            var convo = await env.CreateConversationAsync(sourceRoom);

            if (roomHubMatches is not null)
            {
                var roomHub = roomHubMatches == true
                    ? hub
                    : await env.CreateHubAsync("room-hub");
                sourceRoom.Hub = roomHub;
                await env.Db.SaveChangesAsync();
            }
            if (defaultHubMatches is not null)
            {
                var defaultHub = defaultHubMatches == true
                    ? hub
                    : await env.CreateHubAsync("default-hub");
                hub.Organization.Settings = new()
                {
                    DefaultHubId = defaultHub,
                };
                await env.Db.SaveChangesAsync();
            }

            var message = new NewConversation(
                convo,
                env.TestData.Organization,
                hub,
                new Uri("https://example.com"));

            var apiToken = env.TestData.Organization.RequireAndRevealApiToken();
            var expectedMessageText =
                $"A new conversation was posted by {convo.StartedBy.DisplayName} in {convo.Room.Name}";

            env.SlackApi.AddMessageInfoResponse(
                apiToken,
                expectedMessageText,
                new MessageResponse()
                {
                    Ok = true,
                    Timestamp = "the-hub-thread-id",
                });

            await env.PublishAndWaitForConsumptionAsync(message);

            await env.ReloadAsync(convo);
            await Verify(env, convo)
                .UseParameters(
                    defaultHubMatches switch { null => "NoDefaultHub", true => "MatchingDefaultHub", false => "MismatchingDefaultHub" },
                    roomHubMatches switch { null => "NoRoomHub", true => "MatchingRoomHub", false => "MismatchingRoomHub" });
        }

        [Fact]
        public async Task NoOpsIfConversationAlreadyAttachedToHub()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AttachConversationToHubConsumer>()
                .Build(snapshot: true);

            var sourceRoom = await env.CreateRoomAsync("Csource-room", "source-room");
            var hub1 = await env.CreateHubAsync("test-hub-1", "Chub1-room");
            var hub2 = await env.CreateHubAsync("test-hub-2", "Chub2-room");
            var convo = await env.CreateConversationAsync(sourceRoom);
            convo.HubId = hub2.Id;
            sourceRoom.HubId = hub1.Id;
            await env.Db.SaveChangesAsync();

            var message = new NewConversation(
                convo,
                env.TestData.Organization,
                hub1,
                new Uri("https://example.com"));

            await env.PublishAndWaitForConsumptionAsync(message);

            // No errors were logged
            Assert.Empty(env.GetAllLogs<AttachConversationToHubConsumer>(LogLevel.Error));

            await env.ReloadAsync(convo);
            await Verify(env, convo);
        }
    }

    [UsesVerify]
    public class WithAnAttachConversationToHubMessage
    {
        [Theory]
        [InlineData(false, true, true, TestOrganizationType.Home, TestMemberType.HomeMember)]
        [InlineData(true, false, true, TestOrganizationType.Home, TestMemberType.HomeMember)]
        [InlineData(true, true, false, TestOrganizationType.Home, TestMemberType.HomeMember)]
        [InlineData(true, true, true, null, TestMemberType.HomeMember)]
        [InlineData(true, true, true, TestOrganizationType.Home, null)]
        [InlineData(true, true, true, TestOrganizationType.Home, TestMemberType.ForeignMember)]
        [InlineData(true, true, true, TestOrganizationType.Foreign, TestMemberType.HomeMember)]
        [InlineData(true, true, true, TestOrganizationType.Foreign, TestMemberType.HomeGuest)]
        public async Task NoOpsIfDataIsIncomplete(bool validConvoId, bool validHubId, bool validOrganizationId,
            TestOrganizationType? actorOrgType, TestMemberType? actorType)
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AttachConversationToHubConsumer>()
                .Build(snapshot: true);

            var sourceRoom = await env.CreateRoomAsync();
            var hub = await env.CreateHubAsync();
            var convo = await env.CreateConversationAsync(sourceRoom);

            var message = new AttachConversationToHub()
            {
                ConversationId = validConvoId
                    ? convo
                    : new Id<Conversation>(99),
                HubId = validHubId
                    ? hub
                    : new Id<Hub>(99),
                OrganizationId = validOrganizationId
                    ? env.TestData.Organization
                    : new Id<Organization>(99),
                ActorMemberId = env.TestData.GetMember(actorType)
                    ?? new Id<Member>(99),
                ActorOrganizationId = env.TestData.GetOrganization(actorOrgType)
                    ?? new Id<Organization>(88),
            };

            await env.PublishAndWaitForConsumptionAsync(message);

            await env.ReloadAsync(convo);
            await Verify(env, convo)
                .UseParameters(
                    validConvoId ? "ValidConvo" : "InvalidConvo",
                    validHubId ? "ValidHub" : "InvalidHub",
                    validOrganizationId ? "ValidOrg" : "InvalidOrg",
                    actorOrgType?.ToString() ?? "NullOrg",
                    actorType?.ToString() ?? "NullActor");
        }

        [Theory]
        [InlineData(TestMemberType.HomeMember)]
        [InlineData(TestMemberType.HomeMember, RoomFlags.IsCommunity)]
        [InlineData(TestMemberType.HomeGuest)]
        [InlineData(TestMemberType.ForeignMember)]
        public async Task AttachesConversationToHub(TestMemberType actorType, RoomFlags roomFlags = default)
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AttachConversationToHubConsumer>()
                .Build(snapshot: true);

            var sourceRoom = await env.CreateRoomAsync(roomFlags);
            var hub = await env.CreateHubAsync();
            var convo = await env.CreateConversationAsync(sourceRoom);

            var actor = env.TestData.GetMember(actorType);
            var message = new AttachConversationToHub()
            {
                ConversationId = convo,
                HubId = hub,
                OrganizationId = env.TestData.Organization,
                ActorMemberId = actor,
                ActorOrganizationId = actor.Organization,
            };

            var apiToken = env.TestData.Organization.RequireAndRevealApiToken();
            var expectedMessageText =
                $"A new conversation was posted by {convo.StartedBy.DisplayName} in {convo.Room.Name}";

            env.SlackApi.AddMessageInfoResponse(
                apiToken,
                expectedMessageText,
                new MessageResponse()
                {
                    Ok = true,
                    Timestamp = "the-hub-thread-id",
                });

            await env.PublishAndWaitForConsumptionAsync(message);

            await env.ReloadAsync(convo);
            await Verify(env, convo)
                .UseParameters(actorType, roomFlags);
        }

        [Fact]
        public async Task NoOpsIfConversationAlreadyAttachedToHub()
        {
            var env = TestEnvironmentBuilder.Create()
                .AddBusConsumer<AttachConversationToHubConsumer>()
                .Build(snapshot: true);

            var sourceRoom = await env.CreateRoomAsync("Csource-room", "source-room");
            var hub1 = await env.CreateHubAsync("test-hub-1", "Chub1-room");
            var hub2 = await env.CreateHubAsync("test-hub-2", "Chub2-room");
            var convo = await env.CreateConversationAsync(sourceRoom);
            convo.HubId = hub2.Id;
            await env.Db.SaveChangesAsync();

            var actor = env.TestData.Member;
            var message = new AttachConversationToHub()
            {
                ConversationId = convo,
                HubId = hub1,
                OrganizationId = env.TestData.Organization,
                ActorMemberId = actor,
                ActorOrganizationId = actor.Organization,
            };

            await env.PublishAndWaitForConsumptionAsync(message);

            // Validate that nothing happened
            await env.ReloadAsync(convo);
            await Verify(env, convo);
        }
    }
}
