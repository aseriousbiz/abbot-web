using System;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Xunit;

public class ConversationViewModelTests
{
    public class TheStatusMessage
    {
        [Theory]
        [InlineData(ConversationState.New, "Posted 3 days ago")]
        [InlineData(ConversationState.Overdue, "Last message 2 days ago")] // This state should not really be possible, but maybe someone removed the SLO?
        [InlineData(ConversationState.NeedsResponse, "Last message 2 days ago")]
        [InlineData(ConversationState.Waiting, "Responded to 2 days ago")]
        [InlineData(ConversationState.Closed, "Closed now")]
        [InlineData(ConversationState.Archived, "Archived 6 hours ago")]
        public void WhenRoomHasNoTimeToRespond(ConversationState state, string expected)
        {
            var clock = new TimeTravelClock();
            var conversation = new Conversation
            {
                State = state,
                LastStateChangeOn = clock.UtcNow.AddDays(-2),
                LastMessagePostedOn = clock.UtcNow.AddDays(-1), // Could have been posted by the customer and not the first responder.
                Created = clock.UtcNow.AddDays(-3),
                ArchivedOn = clock.UtcNow.AddHours(-6),
                ClosedOn = clock.UtcNow,
                Room = new Room()
            };
            var title = new RenderedMessage(new RenderedMessageSpan[] { new PlainTextSpan("Title ") });

            var viewModel = new ConversationViewModel(conversation, title, summary: null, clock);

            Assert.Equal(expected, viewModel.StatusMessage);
        }

        [Theory]
        [InlineData(ConversationState.New, 1, "6 days remaining")]
        [InlineData(ConversationState.New, 8, "1 day past deadline")]
        [InlineData(ConversationState.NeedsResponse, 1, "6 days remaining")]
        [InlineData(ConversationState.NeedsResponse, 8, "1 day past deadline")]
        [InlineData(ConversationState.Overdue, 8, "1 day past deadline")]
        public void WaitingOnFirstResponderWithTimeToRespond(ConversationState state, int lastStateChangedDaysAgo, string expected)
        {
            var clock = new TimeTravelClock();
            clock.Freeze();

            var conversation = new Conversation
            {
                State = state,
                LastStateChangeOn = clock.UtcNow.AddDays(-1 * lastStateChangedDaysAgo),
                LastMessagePostedOn = clock.UtcNow.AddDays(-1 * lastStateChangedDaysAgo),
                Created = clock.UtcNow.AddDays(-3),
                ArchivedOn = clock.UtcNow.AddHours(-6),
                ClosedOn = clock.UtcNow,
                Room = new Room
                {
                    TimeToRespond = new Threshold<TimeSpan>(TimeSpan.FromDays(1), TimeSpan.FromDays(7))
                }
            };
            var title = new RenderedMessage(new RenderedMessageSpan[] { new PlainTextSpan("Title ") });

            var viewModel = new ConversationViewModel(conversation, title, summary: null, clock);

            Assert.Equal(expected, viewModel.StatusMessage);
        }
    }

    public class TheThresholdStatus
    {
        [Theory]
        [InlineData(ConversationState.Overdue, 0, ThresholdStatus.Deadline)] // In this case we only look at the current state and don't need to re-evaluate the time.
        [InlineData(ConversationState.Waiting, 10, ThresholdStatus.Ok)]
        [InlineData(ConversationState.Closed, 10, ThresholdStatus.Ok)]
        [InlineData(ConversationState.Archived, 10, ThresholdStatus.Ok)]
        [InlineData(ConversationState.New, 0, ThresholdStatus.Ok)]
        [InlineData(ConversationState.New, 2, ThresholdStatus.Warning)]
        [InlineData(ConversationState.New, 5, ThresholdStatus.Deadline)]
        [InlineData(ConversationState.NeedsResponse, 0, ThresholdStatus.Ok)]
        [InlineData(ConversationState.NeedsResponse, 2, ThresholdStatus.Warning)]
        [InlineData(ConversationState.NeedsResponse, 5, ThresholdStatus.Deadline)]
        public void ReturnsCorrectStatus(ConversationState state, int lastStateChangedDaysAgo, ThresholdStatus expected)
        {
            var clock = new TimeTravelClock();
            var conversation = new Conversation
            {
                State = state,
                LastStateChangeOn = clock.UtcNow.AddDays(-1 * lastStateChangedDaysAgo),
                LastMessagePostedOn = clock.UtcNow.AddDays(-1 * lastStateChangedDaysAgo),
                Created = clock.UtcNow.AddDays(-1 * lastStateChangedDaysAgo),
                ArchivedOn = clock.UtcNow.AddHours(-1 * lastStateChangedDaysAgo),
                ClosedOn = clock.UtcNow,
                Room = new Room
                {
                    TimeToRespond = new Threshold<TimeSpan>(TimeSpan.FromDays(2), TimeSpan.FromDays(4))
                }
            };
            var title = new RenderedMessage(new RenderedMessageSpan[] { new PlainTextSpan("Title ") });

            var viewModel = new ConversationViewModel(conversation, title, summary: null, clock);

            Assert.Equal(expected, viewModel.ThresholdStatus);
        }
    }
}
