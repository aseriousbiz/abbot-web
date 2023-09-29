using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot;
using Serious.Abbot.Entities;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack.BlockKit;
using Xunit;

public class ConversationStateChangeJobTests
{
    public class TheWakeAsyncMethod
    {
        [Fact]
        public async Task MovesConversationBackToOriginalStateAndDMsTheMemberThatSnoozedTheMessage()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room, startedBy: env.TestData.ForeignMember);
            await env.Conversations.SnoozeConversationAsync(conversation, env.TestData.Member, env.Clock.UtcNow);
            var job = env.Activate<ConversationStateChangeJob>();

            await job.WakeAsync(conversation, env.TestData.Member);

            await env.ReloadAsync(conversation);
            var dm = Assert.Single(env.SlackApi.PostedMessages);
            Assert.NotNull(dm.Blocks);
            var section = Assert.IsType<Section>(Assert.Single(dm.Blocks));
            Assert.NotNull(section.Text);
            var expectedUrl = conversation.GetFirstMessageUrl();
            Assert.Equal($"Hi, you asked me to <{expectedUrl}|snooze a conversation> for an hour. I moved it back to the Needs Response state.", section.Text.Text);
            Assert.Equal(ConversationState.New, conversation.State);
        }

        [Theory]
        [InlineData(ConversationState.Waiting)]
        [InlineData(ConversationState.Closed)]
        public async Task MovesConversationBackToNeedsResponseUnlessItsNotInSnoozedState(ConversationState currentState)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room, startedBy: env.TestData.ForeignMember);
            conversation.State = currentState;
            await env.Db.SaveChangesAsync();
            var job = env.Activate<ConversationStateChangeJob>();

            await job.WakeAsync(conversation, env.TestData.Member);

            await env.ReloadAsync(conversation);
            Assert.Equal(currentState, conversation.State);
        }
    }
}
