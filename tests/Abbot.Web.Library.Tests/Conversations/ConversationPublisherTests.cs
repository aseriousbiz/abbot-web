using MassTransit;
using NSubstitute;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Live;

namespace Abbot.Web.Library.Tests.Conversations;

public class ConversationPublisherTests
{
    public class ThePublishConversationStateChangedAsyncMethod
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CallsListenersFlashesAndPublishes(bool @implicit)
        {
            var listener = Substitute.For<IConversationListener>();
            var publishEndpoint = Substitute.For<IPublishEndpoint>();
            var flashPublisher = Substitute.For<IFlashPublisher>();

            var stateChange = new StateChangedEvent
            {
                Conversation = new Conversation
                {
                    Id = 1,
                    OrganizationId = 2,
                    RoomId = 3,
                    HubId = 4,
                },
                OldState = ConversationState.Snoozed,
                NewState = ConversationState.Waiting,
                Implicit = @implicit,
                Created = new DateTime(2005, 5, 5),
                MessageId = "6",
                ThreadId = "7",
                MessageUrl = new Uri("https://example.com"),
                Member = new Member
                {
                    Id = 8,
                    OrganizationId = 9,
                },
            };
            var publisher = new ConversationPublisher(new[] { listener }, publishEndpoint, flashPublisher);

            await publisher.PublishConversationStateChangedAsync(stateChange);

            await listener.Received().OnStateChangedAsync(stateChange);

            await publishEndpoint.Received().Publish(
                Arg.Do<ConversationStateChanged>(published => {
                    Assert.Equal(stateChange.Conversation.Id, published.Conversation.Id);
                    Assert.Equal(stateChange.Conversation.OrganizationId, published.Conversation.OrganizationId);
                    Assert.Equal(stateChange.Conversation.RoomId, published.Conversation.RoomId);
                    Assert.Equal(stateChange.Conversation.HubId, published.Conversation.HubId);
                    Assert.Equal(stateChange.OldState, published.OldState);
                    Assert.Equal(stateChange.NewState, published.NewState);
                    Assert.Equal(@implicit, published.Implicit);
                    Assert.Equal(stateChange.Created, published.Timestamp);
                    Assert.Equal(stateChange.MessageId, published.MessageId);
                    Assert.Equal(stateChange.ThreadId, published.ThreadId);
                    Assert.Equal(stateChange.MessageUrl, published.MessageUrl);
                    Assert.Equal(stateChange.Member.Id, published.Actor.Id);
                    Assert.Equal(stateChange.Member.OrganizationId, published.Actor.OrganizationId);
                }));

            await flashPublisher.Received()
                .PublishAsync(
                    FlashName.ConversationListUpdated,
                    FlashGroup.Organization(new(stateChange.Conversation.OrganizationId)));
        }
    }
}
