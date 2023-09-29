using Abbot.Common.TestHelpers;
using Abbot.Common.TestHelpers.Fakes;
using NSubstitute;
using Segment;
using Serious.Abbot.Events;
using Serious.Abbot.PayloadHandlers;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

public class HandlerDispatcherTests
{
    public class TheInvokeAsyncMethod
    {
        [Fact]
        public async Task WithPlatformMessageInvokesCallsOnMessageInteractionAsyncOnHandler()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var platformMessage = env.CreatePlatformMessage(room, callbackInfo: callbackInfo);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.InvokeAsync(platformMessage);

            Assert.True(handler.OnMessageInteractionCalled);
        }

        [Fact]
        public async Task DoesNotDispatchWhenOrganizationNotEnabled()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Enabled = false;
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var platformMessage = env.CreatePlatformMessage(
                room,
                callbackInfo: callbackInfo);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.InvokeAsync(platformMessage);

            Assert.False(handler.OnMessageInteractionCalled);
        }

        [Fact]
        public async Task WithViewBlockActionsPayloadInvokesCallsOnInteractionAsyncOnHandler()
        {
            var env = TestEnvironment.Create();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new ViewBlockActionsPayload
            {
                View = new ModalView()
                {
                    CallbackId = "view_callback_id",
                },
                Actions = new List<IPayloadElement>
                {
                    new ButtonElement { ActionId = callbackInfo }
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewBlockActionsPayload>(
                payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.InvokeAsync(platformEvent);

            Assert.True(handler.OnInteractionAsyncCalled);
        }

        [Fact]
        public async Task WithViewSubmissionPayloadInvokesCallsOnSubmissionAsyncOnHandler()
        {
            var env = TestEnvironment.Create();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    CallbackId = callbackInfo
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewSubmissionPayload>(payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.InvokeAsync(platformEvent);

            Assert.True(handler.OnSubmissionAsyncCalled);
        }

        [Fact]
        public async Task WithViewClosedPayloadInvokesCallsOnClosedAsyncOnHandler()
        {
            var env = TestEnvironment.Create();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new ViewClosedPayload
            {
                View = new ModalView
                {
                    CallbackId = callbackInfo
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewClosedPayload>(payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(platformEvent).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.InvokeAsync(platformEvent);

            Assert.True(handler.OnClosedAsyncCalled);
        }

        [Fact]
        public async Task WithBlockSuggestionsPayloadInvokesOnBlockSuggestionAsync()
        {
            var env = TestEnvironment.Create();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new BlockSuggestionPayload
            {
                View = new ModalView
                {
                    CallbackId = callbackInfo
                }
            };

            var platformEvent = env.CreateFakePlatformEvent(payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.InvokeAsync(platformEvent);

            Assert.True(handler.OnBlockSuggestionRequestCalled);
        }
    }

    public class TheOnViewInteractionAsyncMethod
    {
        [Fact]
        public async Task DispatchesViewClosedEvent()
        {
            var env = TestEnvironment.Create();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new ViewClosedPayload
            {
                View = new ModalView
                {
                    CallbackId = callbackInfo
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewClosedPayload>(payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.OnViewInteractionAsync(platformEvent);

            Assert.False(handler.OnMessageInteractionCalled);
            Assert.True(handler.OnClosedAsyncCalled);
            Assert.False(handler.OnSubmissionAsyncCalled);
            Assert.False(handler.OnInteractionAsyncCalled);
        }

        [Fact]
        public async Task DoesNotDispatchWhenOrgDisabled()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Enabled = false;
            await env.Db.SaveChangesAsync();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new ViewClosedPayload
            {
                View = new ModalView
                {
                    CallbackId = callbackInfo
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewClosedPayload>(payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.OnViewInteractionAsync(platformEvent);

            Assert.False(handler.OnMessageInteractionCalled);
            Assert.False(handler.OnClosedAsyncCalled);
            Assert.False(handler.OnSubmissionAsyncCalled);
            Assert.False(handler.OnInteractionAsyncCalled);
        }

        [Fact]
        public async Task DispatchesViewInteractionEvent()
        {
            var env = TestEnvironment.Create();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new ViewBlockActionsPayload
            {
                View = new ModalView
                {
                    CallbackId = callbackInfo
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewBlockActionsPayload>(payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.OnViewInteractionAsync(platformEvent);

            Assert.False(handler.OnMessageInteractionCalled);
            Assert.False(handler.OnClosedAsyncCalled);
            Assert.False(handler.OnSubmissionAsyncCalled);
            Assert.True(handler.OnInteractionAsyncCalled);
        }

        [Fact]
        public async Task DispatchesViewSubmissionEvent()
        {
            var env = TestEnvironment.Create();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    CallbackId = callbackInfo
                }
            };
            var platformEvent = env.CreateFakePlatformEvent<IViewSubmissionPayload>(payload);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.OnViewInteractionAsync(platformEvent);

            Assert.False(handler.OnMessageInteractionCalled);
            Assert.False(handler.OnClosedAsyncCalled);
            Assert.True(handler.OnSubmissionAsyncCalled);
            Assert.False(handler.OnInteractionAsyncCalled);
        }
    }

    public class TheOnMessageInteractionAsyncMethod
    {
        [Fact]
        public async Task DispatchesMessageInteraction()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var platformMessage = env.CreatePlatformMessage(room, callbackInfo: callbackInfo);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.OnMessageInteractionAsync(platformMessage);

            Assert.True(handler.OnMessageInteractionCalled);
            Assert.False(handler.OnClosedAsyncCalled);
            Assert.False(handler.OnSubmissionAsyncCalled);
            Assert.False(handler.OnInteractionAsyncCalled);
        }

        [Fact]
        public async Task DoesNotDispatchesMessageInteractionWhenOrganizationDisabled()
        {
            var env = TestEnvironment.Create();
            env.TestData.Organization.Enabled = false;
            await env.Db.SaveChangesAsync();
            var room = await env.CreateRoomAsync();
            var callbackInfo = InteractionCallbackInfo.For<FakeHandler>();
            var platformMessage = env.CreatePlatformMessage(room, callbackInfo: callbackInfo);
            var registry = Substitute.For<IHandlerRegistry>();
            var handler = new FakeHandler();
            registry.Retrieve(callbackInfo).Returns(handler);
            var dispatcher = new HandlerDispatcher(registry, Substitute.For<IAnalyticsClient>());

            await dispatcher.OnMessageInteractionAsync(platformMessage);

            Assert.False(handler.OnMessageInteractionCalled);
            Assert.False(handler.OnClosedAsyncCalled);
            Assert.False(handler.OnSubmissionAsyncCalled);
            Assert.False(handler.OnInteractionAsyncCalled);
        }
    }
}
