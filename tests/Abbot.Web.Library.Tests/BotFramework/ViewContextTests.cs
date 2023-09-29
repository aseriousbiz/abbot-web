using Abbot.Common.TestHelpers.Fakes;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Serious.TestHelpers;

public class ViewContextTests
{
    public class ThePushModalViewAsyncMethod
    {
        [Fact]
        public async Task PopulatesTriggerIdAndCallbackIdAutomatically()
        {
            var view = new ModalView();
            var platformEvent = new FakePlatformEvent<IViewBlockActionsPayload>(
                new ViewBlockActionsPayload { TriggerId = "trigger-id" },
                new Member(),
                new Organization());
            var viewContext = new ViewContext<IViewBlockActionsPayload>(platformEvent, new FakeHandler());

            await viewContext.PushModalViewAsync(view);

            var responder = platformEvent.Responder;
            var (_, modal) = responder.PushedModals.Single(m => m.Item1 == "trigger-id");
            Assert.NotNull(modal);
            Assert.Equal("i:FakeHandler", modal.CallbackId);
        }

        [Fact]
        public async Task DoesNotOverrideCallbackId()
        {
            var view = new ModalView
            {
                CallbackId = "i:SomeHandler"
            };
            var platformEvent = new FakePlatformEvent<IViewBlockActionsPayload>(
                new ViewBlockActionsPayload { TriggerId = "trigger-id" },
                new Member(),
                new Organization());
            var viewContext = new ViewContext<IViewBlockActionsPayload>(platformEvent, new FakeHandler());

            await viewContext.PushModalViewAsync(view);

            var responder = platformEvent.Responder;
            var (_, modal) = responder.PushedModals.Single(m => m.Item1 == "trigger-id");
            Assert.NotNull(modal);
            Assert.Equal("i:SomeHandler", modal.CallbackId);
        }
    }

    public class TheUpdateModalViewAsyncMethod
    {
        [Fact]
        public async Task PopulatesViewIdAndCallbackIdAutomatically()
        {
            var view = new ModalView();
            var platformEvent = new FakePlatformEvent<IViewBlockActionsPayload>(
                new ViewBlockActionsPayload { TriggerId = "trigger-id", View = new ModalView { Id = "view-id" } },
                new Member(),
                new Organization());
            var viewContext = new ViewContext<IViewBlockActionsPayload>(platformEvent, new FakeHandler());

            await viewContext.UpdateModalViewAsync(view);

            var responder = platformEvent.Responder;
            Assert.False(responder.OpenModals.ContainsKey("trigger-id"));
            var modal = responder.OpenModals["view-id"];
            Assert.NotNull(modal);
            Assert.Equal("i:FakeHandler", modal.CallbackId);
        }

        [Fact]
        public async Task DoesNotOverrideCallbackId()
        {
            var view = new ModalView
            {
                CallbackId = "i:SomeHandler"
            };
            var platformEvent = new FakePlatformEvent<IViewBlockActionsPayload>(
                new ViewBlockActionsPayload { TriggerId = "trigger-id", View = new ModalView { Id = "view-id" } },
                new Member(),
                new Organization());
            var viewContext = new ViewContext<IViewBlockActionsPayload>(platformEvent, new FakeHandler());

            await viewContext.UpdateModalViewAsync(view);

            var responder = platformEvent.Responder;
            var modal = responder.OpenModals["view-id"];
            Assert.NotNull(modal);
            Assert.Equal("i:SomeHandler", modal.CallbackId);
        }
    }
}
