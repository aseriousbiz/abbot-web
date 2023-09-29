using Serious.Abbot.Entities;
using Xunit;

public class ConversationExtensionsTests
{
    public class TheGetMessageUrlMethod
    {
        [Theory]
        [InlineData(null, "https://test.slack.com/archives/Croom/p11112222")]
        [InlineData("", "https://test.slack.com/archives/Croom/p11112222")]
        [InlineData("3333.4444", "https://test.slack.com/archives/Croom/p33334444?thread_ts=1111.2222")]
        public void ReturnsFormattedMessageFromConversation(string? messageId, string expected)
        {
            var conversation = new Conversation
            {
                FirstMessageId = "1111.2222",
                Organization = new()
                {
                    Domain = "test.slack.com",
                },
                Room = new()
                {
                    PlatformRoomId = "Croom",
                },
            };

            var result = conversation.GetMessageUrl(messageId);

            Assert.Equal(expected, result.ToString());
        }
    }

    public class TheIsWaitingForResponseMethod
    {
        [Theory]
        [InlineData(ConversationState.New, true)]
        [InlineData(ConversationState.Snoozed, true)]
        [InlineData(ConversationState.Archived, false)]
        [InlineData(ConversationState.Closed, false)]
        [InlineData(ConversationState.Overdue, true)]
        [InlineData(ConversationState.Unknown, false)]
        [InlineData(ConversationState.Waiting, false)]
        [InlineData(ConversationState.NeedsResponse, true)]
        public void ReturnsTrueWhenWaitingForResponse(ConversationState state, bool expected)
        {
            var conversation = new Conversation
            {
                State = state
            };

            Assert.Equal(expected, conversation.State.IsWaitingForResponse());
        }
    }

    public class TheToDisplayNameMethod
    {
        [Theory]
        [InlineData(ConversationState.New, "New")]
        [InlineData(ConversationState.Snoozed, "Snoozed")]
        [InlineData(ConversationState.Archived, "Archived")]
        [InlineData(ConversationState.Closed, "Closed")]
        [InlineData(ConversationState.Overdue, "Overdue")]
        [InlineData(ConversationState.Unknown, "Unknown")]
        [InlineData(ConversationState.Waiting, "Responded")]
        [InlineData(ConversationState.NeedsResponse, "Needs Response")]
        public void ReturnsExpectedDisplayName(ConversationState state, string expected)
        {
            var conversation = new Conversation
            {
                State = state
            };

            Assert.Equal(expected, conversation.State.ToDisplayString());
        }
    }

    public class TheIsOpenMethod
    {
        [Theory]
        [InlineData(ConversationState.New, true)]
        [InlineData(ConversationState.Snoozed, true)]
        [InlineData(ConversationState.Waiting, true)]
        [InlineData(ConversationState.Overdue, true)]
        [InlineData(ConversationState.NeedsResponse, true)]
        [InlineData(ConversationState.Unknown, false)]
        [InlineData(ConversationState.Archived, false)]
        [InlineData(ConversationState.Closed, false)]
        public void ReturnsTrueWhenOpenAndFalseWhenClosed(ConversationState conversationState, bool expected)
        {
            var result = conversationState.IsOpen();
            Assert.Equal(expected, result);
        }
    }
}
