using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Humanizer;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Collections;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Xunit;

namespace Abbot.Web.Library.Tests.Handlers;

public class AppHomePageHandlerTests
{
    public class TheOnInteractionAsyncMethod
    {
        [Theory]
        [InlineData(ConversationState.New, ConversationState.Closed)]
        [InlineData(ConversationState.New, ConversationState.Archived)]
        [InlineData(ConversationState.Closed, ConversationState.Waiting)]
        [InlineData(ConversationState.Archived, ConversationState.Closed)]
        public async Task TransitionsConversationIfSetConversationStateActionIsTriggered(ConversationState startState, ConversationState desiredState)
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var convo = await env.CreateConversationAsync(room);
            convo.State = startState;
            await env.Db.SaveChangesAsync();

            // Move the clock forward
            env.Clock.AdvanceBy(TimeSpan.FromDays(1));

            var handler = env.Activate<AppHomePageHandler>();

            var menu = new OverflowMenu()
            {
                ActionId = $"{AppHomePageHandler.ActionIds.ConversationAction}",
                SelectedOption = new OverflowOption("", $"{AppHomePageHandler.SetConversationStatePrefix}{desiredState}")
            };
            ((IPayloadElement)menu).BlockId = $"{AppHomePageHandler.BlockIds.ConversationPrefix}{convo.Id}";

            var viewContext = env.CreateFakeViewContext(
                new ViewBlockActionsPayload()
                {
                    Actions = new[] { menu }
                },
                handler,
                env.TestData.Member);

            await handler.OnInteractionAsync(viewContext);

            await env.ReloadAsync(convo);
            Assert.Equal(desiredState, convo.State);
            Assert.Equal(env.Clock.UtcNow, convo.LastStateChangeOn);

            // Make sure the view was republished
            Assert.Single(env.SlackApi.PostedAppHomes);
        }
    }

    public class ThePublishAppHomePageAsyncMethod
    {
        static readonly ConversationStats DummyStats = new(new Dictionary<ConversationState, int>(), 0);

        [Fact]
        public async Task RendersARowForEachConversation()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(room, env.TestData.Member, RoomRole.EscalationResponder, env.TestData.Member);

            var convos = new[]
            {
                await env.CreateConversationAsync(room,
                    startedBy: env.TestData.ForeignMember,
                    title: "Convo 6",
                    timestamp: env.Clock.UtcNow.AddDays(-1)),
                await env.CreateConversationAsync(room,
                    startedBy: env.TestData.ForeignMember,
                    title: "Convo 5",
                    timestamp: env.Clock.UtcNow.AddDays(-2)),
                await env.CreateConversationAsync(room,
                    startedBy: env.TestData.ForeignMember,
                    title: "Convo 4",
                    timestamp: env.Clock.UtcNow.AddDays(-3)),
                await env.CreateConversationAsync(room,
                    startedBy: env.TestData.ForeignMember,
                    title: "Convo 3",
                    timestamp: env.Clock.UtcNow.AddDays(-4)),
                await env.CreateConversationAsync(room,
                    startedBy: env.TestData.ForeignMember,
                    title: "Convo 2",
                    timestamp: env.Clock.UtcNow.AddDays(-5)),
            };

            env.Conversations.FakeQueryResults = new(new PaginatedList<Conversation>(convos, 5, 1, 5), DummyStats);

            var handler = env.Activate<AppHomePageHandler>();
            await handler.PublishAppHomePageAsync(
                new BotChannelUser(null, null, null, env.Secret("api-token")),
                env.TestData.Organization,
                env.TestData.Member);

            var publishedView = Assert.Single(env.SlackApi.PostedAppHomes);
            Assert.Equal(17, publishedView.View.Blocks.Count);
            Assert.IsType<Header>(publishedView.View.Blocks[0]);

            var i = 1;
            foreach (var convo in convos.Take(5))
            {
                Assert.IsType<Divider>(publishedView.View.Blocks[i++]);
                var section = Assert.IsType<Section>(publishedView.View.Blocks[i++]);
                Assert.Equal($"conversation:{convo.Id}", section.BlockId);
                Assert.Equal(
                    $"""
                    {env.TestData.ForeignUser.ToMention()} in {room.ToMention()} {convo.Created.Humanize()}
                    {convo.Title}
                    """,
                    section.Text?.Text);

                var overflow = Assert.IsType<OverflowMenu>(section.Accessory);
                Assert.Equal("conversation_action", overflow.ActionId);

                var context = Assert.IsType<Context>(publishedView.View.Blocks[i++]);
                var element = Assert.IsType<MrkdwnText>(Assert.Single(context.Elements));
                Assert.Equal(
                    $"<{convo.GetFirstMessageUrl()}|Go to thread> • " +
                    $"<https://app.ab.bot/conversations/{convo.Id}|View on ab.bot> • " +
                    $"Last message posted {convo.LastMessagePostedOn.Humanize()}",
                    element.Text);
            }

            Assert.IsType<Actions>(publishedView.View.Blocks[i]);
        }

        [Theory]
        [InlineData(ConversationState.Archived, false, false, false, true)]
        [InlineData(ConversationState.Closed, false, true, true, false)]
        [InlineData(ConversationState.New, true, false, true, false)]
        [InlineData(ConversationState.Overdue, true, false, true, false)]
        [InlineData(ConversationState.Unknown, false, false, false, false)]
        [InlineData(ConversationState.Waiting, true, false, true, false)]
        [InlineData(ConversationState.NeedsResponse, true, false, true, false)]
        public async Task RendersCorrectOptionsBasedOnState(ConversationState state, bool closeConversation, bool reopenConversation, bool archiveConversation, bool unarchiveConversation)
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync();
            await env.Rooms.AssignMemberAsync(room, env.TestData.Member, RoomRole.EscalationResponder, env.TestData.Member);

            var convo = await env.CreateConversationAsync(room,
                startedBy: env.TestData.ForeignMember,
                title: "Convo",
                timestamp: env.Clock.UtcNow.AddDays(-1));
            convo.State = state;
            await env.Db.SaveChangesAsync();

            env.Conversations.FakeQueryResults = new(new PaginatedList<Conversation>(new[] { convo }, 1, 1, 5), DummyStats);

            var handler = env.Activate<AppHomePageHandler>();
            await handler.PublishAppHomePageAsync(
                new BotChannelUser(null, null, null, env.Secret("api-token")),
                env.TestData.Organization,
                env.TestData.Member);

            var publishedView = Assert.Single(env.SlackApi.PostedAppHomes);

            var section = Assert.IsType<Section>(publishedView.View.Blocks[2]);
            var overflow = Assert.IsType<OverflowMenu>(section.Accessory);

            Assert.Equal(closeConversation, overflow.Options.Any(o => o.Value == "set_conversation_state:Closed" && o.Text == "✅ Close Conversation"));
            Assert.Equal(reopenConversation, overflow.Options.Any(o => o.Value == "set_conversation_state:Waiting" && o.Text == "✅ Reopen Conversation"));
            Assert.Equal(archiveConversation, overflow.Options.Any(o => o.Value == "set_conversation_state:Archived" && o.Text == "🚫 Stop Tracking Conversation"));
            Assert.Equal(unarchiveConversation, overflow.Options.Any(o => o.Value == "set_conversation_state:Closed" && o.Text == "🚫 Unarchive Conversation"));
        }
    }
}
