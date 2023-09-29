using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

public class PlatformMessageTests
{
    public class TheIgnoreProperty
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SetsPayloadPropertyToMessagePayload(bool ignore)
        {
            var messageEventPayload = new MessageEventInfo(
                string.Empty,
                "C001",
                "U001",
                Array.Empty<string>(),
                DirectMessage: true,
                ignore,
                null,
                null,
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                WorkflowMessage: false);

            var message = new PlatformMessage(
                messageEventPayload,
                null,
                new Organization(),
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member(),
                new BotChannelUser("T001", "B001", "U002"),
                Enumerable.Empty<Member>(),
                new Room { PlatformRoomId = "C001", Name = "the-room" });

            Assert.Equal(ignore, message.Payload.Ignore);
        }
    }

    public class TheShouldBeHandledByAbbotMethod
    {
        [Theory]
        [InlineData(true, false, false, true)]
        [InlineData(true, true, false, true)]
        [InlineData(true, true, true, true)]
        [InlineData(false, false, false, false)]
        [InlineData(false, true, false, true)]
        [InlineData(false, false, true, true)]
        public async Task ReturnsTrueWhenMessageIntendedForAbbot(
            bool directMessage,
            bool directedAtBot,
            bool isPatternMatch,
            bool expected)
        {
            var message = Substitute.For<IPlatformMessage>();
            message.DirectMessage.Returns(directMessage);
            var routeResult = new RouteResult(null!, null, directedAtBot, isPatternMatch);

            var result = message.ShouldBeHandledByAbbot(routeResult);

            Assert.Equal(expected, result);
        }
    }

    public class TheShouldTrackMessageMethod
    {
        [Theory]
        [InlineData(true, false, false, true)]
        [InlineData(true, true, false, true)]
        [InlineData(false, false, false, false)]
        [InlineData(false, true, false, false)]
        [InlineData(true, false, true, false)]
        public async Task ReturnsTrueWhenOrganizationEnabledAndMessageNotDirectedAtAbbot(
            bool orgEnabled,
            bool directMessage,
            bool directedAtBot,
            bool expected)
        {
            var message = Substitute.For<IPlatformMessage>();
            message.From.Returns(new Member { OrganizationId = 23 });
            message.DirectMessage.Returns(directMessage);
            message.Organization.Returns(new Organization { Id = 23, Enabled = orgEnabled });
            var routeResult = new RouteResult(null!, null, directedAtBot, IsPatternMatch: true);

            var result = message.ShouldTrackMessage(routeResult);

            Assert.Equal(expected, result);
        }
    }

    public class TheReplyInThreadMessageTargetProperty
    {
        [Theory]
        [InlineData("8675309", null, "Room/C001(THREAD:8675309)")]
        [InlineData("8675309", "4815162342", "Room/C001(THREAD:4815162342)")]
        public void ReturnsCorrectReplyInThreadMessageTarget(string messageId, string? threadId, string expectedChatAddress)
        {
            var messageEventPayload = new MessageEventInfo(
                string.Empty,
                "C001",
                "U001",
                Array.Empty<string>(),
                DirectMessage: true,
                false,
                messageId,
                threadId,
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>());

            var message = new PlatformMessage(
                messageEventPayload,
                null,
                new Organization(),
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member(),
                new BotChannelUser("T001", "B001", "U002"),
                Enumerable.Empty<Member>(),
                new Room { PlatformRoomId = "C001", Name = "the-room" });

            Assert.NotNull(message.ReplyInThreadMessageTarget);
            Assert.Equal(expectedChatAddress, message.ReplyInThreadMessageTarget.Address.ToString());
        }

        [Fact]
        public void ReturnsNullWhenNoMessageIdAndThreadId()
        {
            var messageEventPayload = new MessageEventInfo(
                string.Empty,
                "C001",
                "U001",
                Array.Empty<string>(),
                DirectMessage: true,
                false,
                null,
                null,
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>());

            var message = new PlatformMessage(
                messageEventPayload,
                null,
                new Organization(),
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member(),
                new BotChannelUser("T001", "B001", "U002"),
                Enumerable.Empty<Member>(),
                new Room { PlatformRoomId = "C001", Name = "the-room" });

            Assert.Null(message.ReplyInThreadMessageTarget);
        }
    }
}
