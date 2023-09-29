using System.Collections.Generic;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Messages;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

public class TicketModalServiceTests
{
    static async Task<TicketingIntegration> CreateZendeskIntegrationAsync(TestEnvironmentWithData env)
    {
        var integration = await env.Integrations.CreateIntegrationAsync(env.TestData.Organization, IntegrationType.Zendesk);
        var settings = new ZendeskSettings
        {
        };
        return new(integration, settings);
    }

    public class TheOnMessageInteractionAsyncMethod
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatesConversationForHomeOrg(bool includeContextId)
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateZendeskIntegrationAsync(env);
            var messageId = "12341234123.21341";
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            var room = await env.CreateRoomAsync(platformRoomId: "C00000123");
            var callbackInfo = InteractionCallbackInfo.For<CreateZendeskTicketFormModal>(includeContextId ? $"{ticketing.Integration.Id}" : null);
            var interactionInfo = new MessageInteractionInfo(
                new MessageBlockActionsPayload
                {
                    TriggerId = "SOME_TRIGGER_ID",
                    Container = new MessageContainer(messageId, false, room.PlatformRoomId),
                    Actions = new List<IPayloadElement>
                    {
                        new ButtonElement
                        {
                            ActionId = callbackInfo,
                            Value = new ConversationIdentifier(room.PlatformRoomId, messageId),
                        }
                    }
                },
                "",
                callbackInfo);
            var msg = env.CreatePlatformMessage(
                room,
                from: actor,
                interactionInfo: interactionInfo,
                messageId: messageId);
            TicketingIntegration? actualTicketing = null;
            Conversation? conversation = null;
            Member? interactor = null;
            var service = env.Activate<TicketModalService>();

            await service.OnMessageInteractionAsync(msg, IntegrationType.Zendesk, async (ticketing, convo, member) => {
                actualTicketing = ticketing;
                conversation = convo;
                interactor = member;
                return new ViewUpdatePayload("modal");
            });

            Assert.Equal(ticketing.Integration, actualTicketing?.Integration);
            Assert.NotNull(conversation);
            Assert.Equal(organization.Id, conversation.OrganizationId);
            Assert.Equal(messageId, conversation.FirstMessageId);
            Assert.NotNull(interactor);
            Assert.Equal(actor.Id, interactor.Id);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatesConversationForForeignActor(bool includeContextId)
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateZendeskIntegrationAsync(env);
            var messageId = "12341234123.21341";
            var organization = env.TestData.Organization;
            var actor = env.TestData.ForeignMember;
            var room = await env.CreateRoomAsync(platformRoomId: "C00000123");
            var callbackInfo = InteractionCallbackInfo.For<CreateZendeskTicketFormModal>(includeContextId ? $"{ticketing.Integration.Id}" : null);
            var interactionInfo = new MessageInteractionInfo(
                new MessageBlockActionsPayload
                {
                    TriggerId = "SOME_TRIGGER_ID",
                    Container = new MessageContainer(messageId, false, room.PlatformRoomId),
                    Actions = new List<IPayloadElement>
                    {
                        new ButtonElement
                        {
                            ActionId = callbackInfo,
                            Value = new ConversationIdentifier(room.PlatformRoomId, messageId),
                        }
                    }
                },
                "",
                callbackInfo);
            var msg = env.CreatePlatformMessage(
                room,
                from: actor,
                interactionInfo: interactionInfo,
                messageId: messageId);
            TicketingIntegration? actualTicketing = null;
            Conversation? conversation = null;
            Member? interactor = null;
            var service = env.Activate<TicketModalService>();

            await service.OnMessageInteractionAsync(msg, IntegrationType.Zendesk, async (ticketing, convo, member) => {
                actualTicketing = ticketing;
                conversation = convo;
                interactor = member;
                return new ViewUpdatePayload("modal");
            });

            Assert.Equal(ticketing.Integration, actualTicketing?.Integration);
            Assert.NotNull(conversation);
            Assert.Equal(organization.Id, conversation.OrganizationId);
            Assert.Equal(messageId, conversation.FirstMessageId);
            Assert.NotNull(interactor);
            Assert.Equal(actor.Id, interactor.Id);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatesConversationForForeignActorInSharedRoomWithBothOrgsHavingAbbot(bool includeContextId)
        {
            var env = TestEnvironment.Create();
            var ticketing = await CreateZendeskIntegrationAsync(env);
            var messageId = "12341234123.21341";
            var organization = env.TestData.Organization;
            var actor = env.TestData.ForeignMember;
            var room = await env.CreateRoomAsync(platformRoomId: "C00000123");
            room = await env.CreateRoomAsync(platformRoomId: "C00000123", org: env.TestData.ForeignOrganization);
            var callbackInfo = InteractionCallbackInfo.For<CreateZendeskTicketFormModal>(includeContextId ? $"{ticketing.Integration.Id}" : null);
            var interactionInfo = new MessageInteractionInfo(
                new MessageBlockActionsPayload
                {
                    TriggerId = "SOME_TRIGGER_ID",
                    Container = new MessageContainer(messageId, false, room.PlatformRoomId),
                    Actions = new List<IPayloadElement>
                    {
                        new ButtonElement
                        {
                            ActionId = callbackInfo,
                            Value = new ConversationIdentifier(room.PlatformRoomId, messageId),
                        }
                    }
                },
                "",
                callbackInfo);
            var msg = env.CreatePlatformMessage(
                room,
                from: actor,
                interactionInfo: interactionInfo,
                messageId: messageId);
            TicketingIntegration? actualTicketing = null;
            Conversation? conversation = null;
            Member? interactor = null;
            var service = env.Activate<TicketModalService>();

            await service.OnMessageInteractionAsync(msg, IntegrationType.Zendesk, async (ticketing, convo, member) => {
                actualTicketing = ticketing;
                conversation = convo;
                interactor = member;
                return new ViewUpdatePayload("modal");
            });

            Assert.Equal(ticketing.Integration, actualTicketing?.Integration);
            Assert.NotNull(conversation);
            Assert.Equal(organization.Id, conversation.OrganizationId);
            Assert.Equal(messageId, conversation.FirstMessageId);
            Assert.NotNull(interactor);
            Assert.Equal(actor.Id, interactor.Id);
        }
    }
}
