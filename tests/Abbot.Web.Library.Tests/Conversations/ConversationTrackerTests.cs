using Abbot.Common.TestHelpers;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.AI;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Live;
using Serious.Abbot.Models;
using Serious.Abbot.Services;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;
using Serious.TestHelpers;

public class ConversationTrackerTests
{
    [Fact]
    public async Task TheThreadIdsSanityTest()
    {
        var env = TestEnvironment.Create();
        var room = await env.CreateRoomAsync(
            persistent: true,
            managedConversationsEnabled: true);
        var convo = await env.CreateConversationAsync(room, startedBy: env.TestData.ForeignMember, createFirstMessageEvent: true);
        convo.ThreadIds.Add("123412343.343536");
        await env.Db.SaveChangesAsync();
        // Force a new DI scope to be created
        using var _ = env.GetRequiredServiceInNewScope<AbbotContext>(out var dbContext);
        var c = await dbContext.Conversations.Where(c => c.ThreadIds.Contains("123412343.343536")).SingleOrDefaultAsync();
        Assert.NotNull(c);
        Assert.Equal(new[] { c.FirstMessageId, "123412343.343536" }, c.ThreadIds.ToArray());
    }

    public class TheUpdateConversationAsyncMethod
    {
        [Theory]
        [InlineData(PlanType.Free, true, true)]
        [InlineData(PlanType.Business, false, false)]
        public async Task NoOpsIfPreconditionsInvalid(PlanType planType, bool persistentRoom, bool managedConversationsEnabled)
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.PlanType = planType;
            var room = await env.CreateRoomAsync(
                persistent: persistentRoom,
                managedConversationsEnabled: managedConversationsEnabled);
            var convo = await env.CreateConversationAsync(room, startedBy: env.TestData.ForeignMember, createFirstMessageEvent: true);
            Assert.Equal(1, convo.Members.Count);
            Assert.Equal(1, (await env.Conversations.GetTimelineAsync(convo)).Count);

            await env.Db.SaveChangesAsync();
            var tracker = env.Activate<ConversationTracker>();
            var message = new ConversationMessage(
                "A message",
                env.TestData.Organization,
                env.TestData.Member,
                room,
                DateTime.UtcNow,
                "1234",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);

            Assert.Null(await env.Rooms.GetRoomByPlatformRoomIdAsync("C123", env.TestData.Organization));
            await tracker.UpdateConversationAsync(convo, message);
            await env.ReloadAsync(convo);
            Assert.Equal(1, convo.Members.Count);
            Assert.Equal(1, (await env.Conversations.GetTimelineAsync(convo)).Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UpdatesExistingConversationWhenMessageIsThreadInExistingConversation(
            bool managedConversationsEnabled)
        {
            // We're not testing the full state machine here.
            // The ConversationRepositoryTests do a pretty good job of that.
            // This is just a test to make sure we _are_ flowing through that state machine.

            var env = TestEnvironment.Create();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: managedConversationsEnabled);
            var conversation = await env.CreateConversationAsync(room, createFirstMessageEvent: true);

            Assert.Equal(ConversationState.New, conversation.State);
            Assert.Equal(1, (await env.Conversations.GetTimelineAsync(conversation)).Count);

            // Post a message as the foreign member
            await tracker.UpdateConversationAsync(conversation, new ConversationMessage(
                "Message 1",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                DateTime.UtcNow,
                "1111.2222",
                "1111.1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null));
            await env.ReloadAsync(conversation);
            Assert.Equal(ConversationState.New, conversation.State);
            Assert.Equal(2, conversation.Members.Count);
            Assert.Equal(2, (await env.Conversations.GetTimelineAsync(conversation)).Count);

            // Post as a home member
            await tracker.UpdateConversationAsync(conversation, new ConversationMessage(
                "Message 2",
                env.TestData.Organization,
                env.TestData.Member,
                room,
                DateTime.UtcNow,
                "1111.3333",
                "1111.1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null));
            await env.ReloadAsync(conversation);
            Assert.Equal(ConversationState.Waiting, conversation.State);
            Assert.Equal(2, conversation.Members.Count);
            Assert.Equal(4, (await env.Conversations.GetTimelineAsync(conversation)).Count);

            // Post as a guest member
            await tracker.UpdateConversationAsync(conversation, new ConversationMessage(
                "Message 3",
                env.TestData.Organization,
                env.TestData.Guest,
                room,
                DateTime.UtcNow,
                "3333.3333",
                "1111.1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null));
            await env.ReloadAsync(conversation);
            Assert.Equal(ConversationState.NeedsResponse, conversation.State);
            Assert.Equal(3, conversation.Members.Count);
            Assert.Equal(6, (await env.Conversations.GetTimelineAsync(conversation)).Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NotifiesListenersOfNewMessage(bool isLive)
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IConversationPublisher, ConversationPublisher>()
                .Build();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);

            // Post a message as the foreign member
            var conversationMessage = new ConversationMessage(
                "Message 1",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                DateTime.UtcNow,
                "1111.2222",
                "1111.1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                isLive ? FakeMessageContext.Create() : null);
            Assert.Equal(isLive, conversationMessage.IsLive);

            await tracker.UpdateConversationAsync(conversation, conversationMessage);

            var (observedConvo, observedMessage) = Assert.Single(env.ConversationListener.NewMessagesObserved);
            Assert.Same(conversation, observedConvo);
            Assert.Same(conversationMessage, observedMessage);
        }

        [Fact]
        public async Task PublishesNewMessageToBusWithCategoriesAndCategorizationTimelineEvent()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationPublisher, ConversationPublisher>()
                .ReplaceService<IConversationRepository, ConversationRepository>()
                .Build();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            conversation.HubId = 42;
            conversation.HubThreadId = "4444.5555";
            await env.Db.SaveChangesAsync();
            // Post a message as the foreign member
            var conversationMessage = new ConversationMessage(
                "Message 1",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                DateTime.UtcNow,
                "1677880536.112979",
                "1677880420.110101",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null)
            {
                ClassificationResult = CreateClassificationResult(
                    new("category1", "value1"),
                    new("category2", "value2"))
            };
            var tracker = env.Activate<ConversationTracker>();

            await tracker.UpdateConversationAsync(conversation, conversationMessage);

            var published = await env.BusTestHarness.Published
                .SelectAsync<NewMessageInConversation>(m => m.Context.Message.MessageId == "1677880536.112979")
                .FirstOrDefault();
            Assert.NotNull(published);

            var message = published.Context.Message;
            Assert.Equal(conversation.Id, message.ConversationId);
            Assert.Equal(room.Id, message.RoomId);
            Assert.Equal(organization.Id, message.OrganizationId);
            Assert.Equal("1677880536.112979", published.Context.Message.MessageId);
            Assert.False(message.IsLive);
            Assert.Equal(conversation.State, message.ConversationState);
            Assert.Equal(new Uri($"https://{organization.Domain}/archives/{room.PlatformRoomId}/p1677880536112979?thread_ts=1677880420.110101"), message.MessageUrl);
            Assert.Equal(conversation.HubId, message.HubId);
            Assert.Equal(conversation.HubThreadId, message.HubThreadId);
            var categories = message.ClassificationResult?.Categories;
            Assert.NotNull(categories);
            Assert.Collection(categories,
                c => Assert.Equal("category1:value1", c.ToString()),
                c => Assert.Equal("category2:value2", c.ToString()));
            var conversationRepository = env.Activate<ConversationRepository>();
            var timelineEvents = await conversationRepository.GetTimelineAsync(conversation);
            Assert.IsType<MessagePostedEvent>(timelineEvents.Last());
        }

        [Fact]
        public async Task PublishesFlashConversationListUpdated()
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IConversationPublisher, ConversationPublisher>()
                .Build();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);

            // Post a message as the foreign member
            var conversationMessage = new ConversationMessage(
                "Message 1",
                env.TestData.Organization,
                env.TestData.ForeignMember,
                room,
                DateTime.UtcNow,
                "1111.2222",
                "1111.1111",
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);

            await tracker.UpdateConversationAsync(conversation, conversationMessage);

            var flash = Assert.Single(env.FlashPublisher.PublishedFlashes);
            Assert.Equal(FlashName.ConversationListUpdated, flash.Name);
            Assert.Equal(FlashGroup.Organization(env.TestData.Organization), flash.Group);
            Assert.Empty(flash.Arguments);
        }
    }

    public class TheTryCreateNewConversationAsyncMethod
    {
        public enum MemberType
        {
            Home,
            Guest,
            Foreign,
        }

        Member GetMember(CommonTestData testData, MemberType memberType) =>
            memberType switch
            {
                MemberType.Guest => testData.Guest,
                MemberType.Foreign => testData.ForeignMember,
                _ => testData.Member,
            };

        [Fact]
        public async Task PublishesNewConversationMessageAndNewMessageInConversationMessageToBusWithCategories()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IConversationPublisher, ConversationPublisher>()
                .ReplaceService<IConversationRepository, ConversationRepository>()
                .Build();
            var organization = env.TestData.Organization;
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            // Post a message as the foreign member
            var conversationMessage = new ConversationMessage(
                "Message 1",
                organization,
                env.TestData.ForeignMember,
                room,
                env.Clock.UtcNow,
                "1677880536.112979",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null)
            {
                ClassificationResult = CreateClassificationResult(
                    new("category1", "value1"),
                    new("sentiment", "hulk-smash"))
            };
            var tracker = env.Activate<ConversationTracker>();

            var conversation = await tracker.TryCreateNewConversationAsync(conversationMessage, env.Clock.UtcNow);

            Assert.NotNull(conversation);
            var publishedNewConversation = await env.BusTestHarness.Published
                .SelectAsync<NewConversation>(m => m.Context.Message.ConversationId.Value == conversation.Id)
                .FirstOrDefault();
            Assert.NotNull(publishedNewConversation);
            var newConversationMessage = publishedNewConversation.Context.Message;
            Assert.Equal(conversation.Id, newConversationMessage.ConversationId);
            Assert.Equal(organization.Id, newConversationMessage.OrganizationId);
            Assert.Equal(new Uri($"https://{organization.Domain}/archives/{room.PlatformRoomId}/p1677880536112979"), newConversationMessage.MessageUrl);
            var publishedNewMessage = await env.BusTestHarness.Published
                .SelectAsync<NewMessageInConversation>(m => m.Context.Message.ConversationId.Value == conversation.Id)
                .FirstOrDefault();
            Assert.NotNull(publishedNewMessage);
            var newMessageMessage = publishedNewMessage.Context.Message;
            Assert.Equal(conversation.Id, newMessageMessage.ConversationId);
            Assert.Equal(organization.Id, newMessageMessage.OrganizationId);
            Assert.Equal(new Uri($"https://{organization.Domain}/archives/{room.PlatformRoomId}/p1677880536112979"), newMessageMessage.MessageUrl);
            var categories = newMessageMessage.ClassificationResult?.Categories;
            Assert.NotNull(categories);
            Assert.Collection(categories,
                c => Assert.Equal("category1:value1", c.ToString()),
                c => Assert.Equal("sentiment:hulk-smash", c.ToString()));
            var conversationRepository = env.Activate<ConversationRepository>();
            var timelineEvents = await conversationRepository.GetTimelineAsync(conversation);
            Assert.IsType<MessagePostedEvent>(timelineEvents.Last());
        }

        [Theory]
        [InlineData(PlanType.Free, true, true, false, MemberType.Foreign, false)] // Not Business
        [InlineData(PlanType.Free, true, true, false, MemberType.Guest, false)]   // Not Business
        [InlineData(PlanType.Free, true, true, false, MemberType.Home, true)]     // Not Business
        [InlineData(PlanType.Business, false, false, true, MemberType.Foreign, false)] // Not persistent (e.g. DM)
        [InlineData(PlanType.Business, false, false, true, MemberType.Guest, false)]   // Not persistent (e.g. DM)
        [InlineData(PlanType.Business, false, false, true, MemberType.Home, true)]     // Not persistent (e.g. DM)
        [InlineData(PlanType.Business, true, false, false, MemberType.Foreign, false)] // Managed Conversations disabled
        [InlineData(PlanType.Business, true, false, false, MemberType.Guest, false)]   // Managed Conversations disabled
        //          PlanType.Business, true, false, false, MemberType.Home, true       // Managed Conversations disabled, but force would override
        [InlineData(PlanType.Business, true, true, true, MemberType.Foreign, false)] // In thread
        [InlineData(PlanType.Business, true, true, true, MemberType.Guest, false)]   // In thread
        //          PlanType.Business, true, true, true, MemberType.Home, true       // In thread, but force would override
        //          PlanType.Business, true, true, true, MemberType.Foreign, false  // Preconditions Met!
        //          PlanType.Business, true, true, true, MemberType.Guest, false    // Preconditions Met!
        [InlineData(PlanType.Business, true, true, true, MemberType.Home, false)]   // Home user without force
        //          PlanType.Business, true, true, true, MemberType.Home, true      // Preconditions Met!
        public async Task NoOpsIfPreconditionsNotMet(PlanType organizationPlanType, bool persistentRoom, bool managedConversationsEnabled, bool isInThread, MemberType memberType, bool force)
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.PlanType = organizationPlanType;
            await env.Db.SaveChangesAsync();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync(
                persistent: persistentRoom,
                managedConversationsEnabled: managedConversationsEnabled);

            var message = new ConversationMessage(
                "The message",
                env.TestData.Organization,
                GetMember(env.TestData, memberType),
                room,
                DateTime.UtcNow,
                "1234",
                isInThread ? "1234.5678" : null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);
            Assert.False(ConversationTracker.ShouldTrackConversation(message, force));

            var convo = await tracker.TryCreateNewConversationAsync(message, DateTime.UtcNow, null, force);
            Assert.Null(convo);
        }

        [Theory]
        [InlineData(MemberType.Foreign, false)]
        [InlineData(MemberType.Foreign, true)]
        [InlineData(MemberType.Guest, false)]
        [InlineData(MemberType.Guest, true)]
        [InlineData(MemberType.Home, true)]
        public async Task CreatesNewConversationForTopLevelMessagesFromValidUsers(MemberType memberType, bool allowNonSupportee)
        {
            var env = TestEnvironment.Create();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            var sender = GetMember(env.TestData, memberType);
            var message = env.CreateFakeMessageContext(
                room: room,
                from: sender,
                timestamp: new DateTimeOffset(1000, 01, 02, 03, 04, 05, TimeSpan.Zero).DateTime,
                messageId: "1234.5678",
                threadId: null);

            var convoMessage = ConversationMessage.CreateFromLiveMessage(message, Array.Empty<SensitiveValue>());
            Assert.True(ConversationTracker.ShouldTrackConversation(convoMessage, allowNonSupportee));

            var convo = await tracker.TryCreateNewConversationAsync(convoMessage, DateTime.UtcNow, null, allowNonSupportee);
            Assert.NotNull(convo);

            var actual = await env.Db.Conversations.SingleAsync();
            Assert.Equal(convo.Id, actual.Id);
            Assert.Equal(message.MessageId, actual.FirstMessageId);
            Assert.Equal(sender.Id, actual.StartedById);
        }

        [Theory]
        [InlineData(MemberType.Foreign, false)]
        [InlineData(MemberType.Foreign, true)]
        [InlineData(MemberType.Guest, false)]
        [InlineData(MemberType.Guest, true)]
        [InlineData(MemberType.Home, true)]
        public async Task CreatesNewConversationForImportedMessageFromForeignUser(MemberType memberType, bool allowNonSupportee)
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);

            var sender = GetMember(env.TestData, memberType);
            var message = new ConversationMessage(
                "The message",
                env.TestData.Organization,
                sender,
                room,
                env.Clock.UtcNow.AddDays(-14),
                "1234",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);

            var convo = await tracker.TryCreateNewConversationAsync(message, env.Clock.UtcNow, null, allowNonSupportee);
            Assert.NotNull(convo);

            Assert.True(ConversationTracker.ShouldTrackConversation(message, allowNonSupportee));

            var actual = await env.Db.Conversations.SingleAsync();
            Assert.Equal(convo.Id, actual.Id);
            Assert.Equal(message.MessageId, actual.FirstMessageId);
            Assert.Equal(env.Clock.UtcNow, actual.ImportedOn);
            Assert.Equal(env.Clock.UtcNow.AddDays(-14), actual.Created);
            Assert.Equal(sender.Id, actual.StartedById);

            // Since this is a replayed message (MessageContext: null)
            // Verify that no signal was raised
            Assert.False(message.IsLive);
            Assert.Empty(env.SignalHandler.RaisedSignals);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task PublishesNewConversationMessageWithExpectedHub(bool hasDefaultHub, bool hasRoomHub)
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IConversationPublisher, ConversationPublisher>()
                .Build();
            env.Clock.Freeze();
            var tracker = env.Activate<ConversationTracker>();
            var actor = env.TestData.Member;
            var defaultHub = await SetDefaultHub(env, actor);
            var room = await env.CreateRoomAsync("Croom", managedConversationsEnabled: true);
            var roomHub = await SetRoomHub(env, room, actor);

            var message = new ConversationMessage(
                "The message",
                env.TestData.Organization,
                env.TestData.Guest,
                room,
                env.Clock.UtcNow.AddDays(-14),
                "1234",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);

            var convo = await tracker.TryCreateNewConversationAsync(message, env.Clock.UtcNow);

            Assert.NotNull(convo);
            var publishedMessage = Assert.Single(env.BusTestHarness.Published.Select(m => m.MessageType == typeof(NewConversation)));
            var messageObject = Assert.IsType<NewConversation>(publishedMessage.MessageObject);
            Assert.Equal(convo.Id, messageObject.ConversationId.Value);
            Assert.Equal(room.OrganizationId, messageObject.OrganizationId.Value);
            Assert.Equal(
                hasRoomHub ? roomHub : hasDefaultHub ? defaultHub : null,
                messageObject.RoomHubId);

            async Task<Hub?> SetDefaultHub(TestEnvironmentWithData e, Member a)
            {
                if (!hasDefaultHub)
                {
                    return null;
                }

                var hub = await e.Hubs.CreateHubAsync("default-hub", await e.CreateRoomAsync(), a);
                await e.Hubs.SetDefaultHubAsync(hub, a);
                return hub;
            }

            async Task<Hub?> SetRoomHub(TestEnvironmentWithData e, Room r, Member a)
            {
                if (!hasRoomHub)
                {
                    return null;
                }

                var hub = await e.Hubs.CreateHubAsync("room-hub", await e.CreateRoomAsync(), a);
                await e.Rooms.AttachToHubAsync(r, hub, a);
                return hub;
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task NotifiesListenersOfNewConversation(bool isLive)
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IConversationPublisher, ConversationPublisher>()
                .Build();
            env.Clock.Freeze();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync("Croom", managedConversationsEnabled: true);

            var message = new ConversationMessage(
                "The message",
                env.TestData.Organization,
                env.TestData.Guest,
                room,
                env.Clock.UtcNow.AddDays(-14),
                "1234",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                isLive ? FakeMessageContext.Create() : null);
            Assert.Equal(isLive, message.IsLive);

            var convo = await tracker.TryCreateNewConversationAsync(message, env.Clock.UtcNow);

            Assert.NotNull(convo);
            var (observedConvo, observedMessage) = Assert.Single(env.ConversationListener.NewConversationsObserved);
            Assert.Same(convo, observedConvo);
            Assert.Same(message, observedMessage);
        }

        [Fact]
        public async Task PublishesFlashConversationListUpdated()
        {
            var env = TestEnvironmentBuilder.Create()
                .ReplaceService<IConversationPublisher, ConversationPublisher>()
                .Build();
            env.Clock.Freeze();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync("Croom", managedConversationsEnabled: true);

            var message = new ConversationMessage(
                "The message",
                env.TestData.Organization,
                env.TestData.Guest,
                room,
                env.Clock.UtcNow.AddDays(-14),
                "1234",
                null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);

            await tracker.TryCreateNewConversationAsync(message, env.Clock.UtcNow);

            var flash = Assert.Single(env.FlashPublisher.PublishedFlashes);
            Assert.Equal(FlashName.ConversationListUpdated, flash.Name);
            Assert.Equal(FlashGroup.Organization(env.TestData.Organization), flash.Group);
            Assert.Empty(flash.Arguments);
        }

        [Theory]
        [InlineData(false, false, MemberType.Foreign)]
        [InlineData(false, false, MemberType.Guest)]
        [InlineData(true, true, MemberType.Foreign)]
        [InlineData(true, true, MemberType.Guest)]
        [InlineData(true, false, MemberType.Home)]
        public async Task CreatesConversationWhenForced(bool managedConversationsEnabled, bool isInThread, MemberType memberType)
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var tracker = env.Activate<ConversationTracker>();
            var room = await env.CreateRoomAsync(
                managedConversationsEnabled: managedConversationsEnabled);

            var sender = GetMember(env.TestData, memberType);
            var message = new ConversationMessage(
                "The message",
                env.TestData.Organization,
                sender,
                room,
                env.Clock.UtcNow.AddDays(-14),
                "1234",
                isInThread ? "1234.5678" : null,
                Array.Empty<ILayoutBlock>(),
                Array.Empty<FileUpload>(),
                null);

            var convo = await tracker.TryCreateNewConversationAsync(
                message,
                env.Clock.UtcNow,
                conversationMatchAIResult: null,
                force: true);

            Assert.NotNull(convo);
            Assert.True(ConversationTracker.ShouldTrackConversation(message, true));

            var actual = await env.Db.Conversations.SingleAsync();
            Assert.Equal(convo.Id, actual.Id);
            Assert.Equal(message.MessageId, actual.FirstMessageId);
            Assert.Equal(env.Clock.UtcNow, actual.ImportedOn);
            Assert.Equal(env.Clock.UtcNow.AddDays(-14), actual.Created);
            Assert.Equal(sender.Id, actual.StartedById);

            // Since this is a "Hidden" message, verify that no signal was raised
            Assert.False(message.IsLive);
            Assert.Equal(
                managedConversationsEnabled ? ConversationState.New : ConversationState.Hidden,
                actual.State);
            Assert.Empty(env.SignalHandler.RaisedSignals);
        }
    }

    public class TheImportConversationAsyncMethod
    {
        [Fact]
        public async Task ThrowsIfNoMessagesProvided()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();

            var tracker = env.Activate<ConversationTracker>();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => tracker.CreateConversationAsync(Array.Empty<ConversationMessage>(), env.TestData.Abbot, env.Clock.UtcNow));
            Assert.Equal("Can’t create a conversation without messages!", ex.Message);
        }

        [Fact]
        public async Task ReturnsExistingConversationIfItExists()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            var existing = await env.CreateConversationAsync(room, firstMessageId: "1111");
            var messages = new List<ConversationMessage>()
            {
                new(
                    "Question?",
                    env.TestData.Organization,
                    env.TestData.ForeignMember,
                    room,
                    env.Clock.UtcNow.AddDays(-10),
                    "1111",
                    "1111",
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    null),
            };

            var tracker = env.Activate<ConversationTracker>();

            var conversation = tracker.CreateConversationAsync(messages, env.TestData.Abbot, env.Clock.UtcNow);
            Assert.Equal(existing.Id, conversation.Id);
        }

        [Fact]
        public async Task CreatesANewConversationAddsImportEventAndClosesIt()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var room = await env.CreateRoomAsync(persistent: true, managedConversationsEnabled: true);
            var messages = new List<ConversationMessage>()
            {
                new(
                    "Question?",
                    env.TestData.Organization,
                    env.TestData.ForeignMember,
                    room,
                    env.Clock.UtcNow.AddDays(-10),
                    "1111",
                    "1111",
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    null),
                new(
                    "Answer.",
                    env.TestData.Organization,
                    env.TestData.Member,
                    room,
                    env.Clock.UtcNow.AddDays(-9),
                    "2222",
                    "1111",
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    null),
                new(
                    "Gratitude.",
                    env.TestData.Organization,
                    env.TestData.ForeignMember,
                    room,
                    env.Clock.UtcNow.AddDays(-8),
                    "3333",
                    "1111",
                    Array.Empty<ILayoutBlock>(),
                    Array.Empty<FileUpload>(),
                    null),
            };

            var tracker = env.Activate<ConversationTracker>();

            var convo = await tracker.CreateConversationAsync(messages, env.TestData.Abbot, env.Clock.UtcNow);
            Assert.NotNull(convo);

            Assert.Equal(env.Clock.UtcNow, convo.ClosedOn);
            Assert.Equal("Question?", convo.Title);
            Assert.Equal("1111", convo.FirstMessageId);
            Assert.Same(env.TestData.ForeignMember, convo.StartedBy);
            Assert.Equal(new[] { env.TestData.ForeignMember.Id, env.TestData.Member.Id },
                convo.Members.Select(m => m.MemberId).ToArray());

            Assert.Collection(await env.Conversations.GetTimelineAsync(convo),
                evt => {
                    var mpe = Assert.IsType<MessagePostedEvent>(evt);
                    Assert.Equal(env.Clock.UtcNow.AddDays(-10), mpe.Created);
                    Assert.Equal(messages[0].MessageId, mpe.MessageId);
                    Assert.Same(env.TestData.ForeignMember, mpe.Member);
                },
                evt => {
                    var mpe = Assert.IsType<MessagePostedEvent>(evt);
                    Assert.Equal(env.Clock.UtcNow.AddDays(-9), mpe.Created);
                    Assert.Equal(messages[1].MessageId, mpe.MessageId);
                    Assert.Same(env.TestData.Member, mpe.Member);
                },
                evt => {
                    var sce = Assert.IsType<StateChangedEvent>(evt);
                    Assert.Equal(env.Clock.UtcNow.AddDays(-9), sce.Created);
                    Assert.Same(env.TestData.Member, sce.Member);
                    Assert.Equal(ConversationState.New, sce.OldState);
                    Assert.Equal(ConversationState.Waiting, sce.NewState);
                },
                evt => {
                    var mpe = Assert.IsType<MessagePostedEvent>(evt);
                    Assert.Equal(env.Clock.UtcNow.AddDays(-8), mpe.Created);
                    Assert.Equal(messages[2].MessageId, mpe.MessageId);
                    Assert.Same(env.TestData.ForeignMember, mpe.Member);
                },
                evt => {
                    var sce = Assert.IsType<StateChangedEvent>(evt);
                    Assert.Equal(env.Clock.UtcNow.AddDays(-8), sce.Created);
                    Assert.Same(env.TestData.ForeignMember, sce.Member);
                    Assert.Equal(ConversationState.Waiting, sce.OldState);
                    Assert.Equal(ConversationState.NeedsResponse, sce.NewState);
                },
                evt => {
                    var sie = Assert.IsType<SlackImportEvent>(evt);
                    Assert.Equal(env.Clock.UtcNow, sie.Created);
                    Assert.Same(env.TestData.Abbot, sie.Member);
                },
                evt => {
                    var sce = Assert.IsType<StateChangedEvent>(evt);
                    Assert.Equal(env.Clock.UtcNow, sce.Created);
                    Assert.Same(env.TestData.Abbot, sce.Member);
                    Assert.Equal(ConversationState.NeedsResponse, sce.OldState);
                    Assert.Equal(ConversationState.Closed, sce.NewState);
                });
        }
    }

    public class TheIsSupporteeMethod
    {
        [Fact]
        public void ReturnsTrueForExternalMember()
        {
            var member = new Member
            {
                IsGuest = false,
                OrganizationId = 42,
            };
            var room = new Room { OrganizationId = 123 };
            Assert.True(ConversationTracker.IsSupportee(member, room));
        }

        [Fact]
        public void ReturnsFalseForHomeOrgMember()
        {
            var member = new Member
            {
                IsGuest = false,
                OrganizationId = 123,
            };
            var room = new Room { OrganizationId = 123 };
            Assert.False(ConversationTracker.IsSupportee(member, room));
        }

        [Fact]
        public void ReturnsTrueForGuest()
        {
            var member = new Member
            {
                IsGuest = true,
                OrganizationId = 123,
            };
            var room = new Room { OrganizationId = 123 };
            Assert.True(ConversationTracker.IsSupportee(member, room));
        }

        [Fact]
        public void ReturnsTrueForNonAgentHomeOrgMemberInCommunityRoom()
        {
            var member = new Member
            {
                IsGuest = false,
                OrganizationId = 123,
            };
            var room = new Room { OrganizationId = 123, Settings = new RoomSettings { IsCommunityRoom = true } };
            Assert.True(ConversationTracker.IsSupportee(member, room));
        }

        [Fact]
        public void ReturnsFalseForAgentHomeOrgMemberInCommunityRoom()
        {
            var member = new Member
            {
                IsGuest = false,
                OrganizationId = 123,
                MemberRoles = new[] { new MemberRole { Role = new Role { Name = "Agent" } } }
            };
            var room = new Room { OrganizationId = 123, Settings = new RoomSettings { IsCommunityRoom = true } };
            Assert.False(ConversationTracker.IsSupportee(member, room));
        }
    }

    static ClassificationResult CreateClassificationResult(params Category[] categories)
    {
        return new ClassificationResult
        {
            Categories = categories,
            RawCompletion = "null",
            PromptTemplate = "null",
            Prompt = new("null"),
            Temperature = 1,
            TokenUsage = new TokenUsage(0, 0, 0),
            Model = "gpt-4",
            ProcessingTime = TimeSpan.Zero,
            Directives = Array.Empty<Directive>(),
            UtcTimestamp = DateTime.UtcNow,
            ReasonedActions = Array.Empty<Reasoned<string>>(),
        };
    }
}
