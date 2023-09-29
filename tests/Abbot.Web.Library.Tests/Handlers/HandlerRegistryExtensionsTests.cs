using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Xunit;

public class HandlerRegistryExtensionsTests
{
    public class TheRetrieveMethod
    {
        [Fact]
        public void RetrievesHandlerForViewHandlerUsingViewCallbackId()
        {
            var payload = new ViewClosedPayload
            {
                View = new ModalView
                {
                    CallbackId = new InteractionCallbackInfo(nameof(Handler1))
                }
            };
            var platformEvent = new PlatformEvent<IViewPayload>(
                payload,
                null,
                new BotChannelUser("B01234", "U01234", "abbot"),
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member(),
                null,
                new Organization());
            var handlers = new IHandler[] { new Handler1(), new Handler2(), new Handler3() };
            var registry = new HandlerRegistry(handlers);

            var handler = registry.Retrieve(platformEvent);

            Assert.IsType<Handler1>(handler);
        }

        [Fact]
        public void RetrievesHandlerForViewHandlerUsingBlockId()
        {
            var button = new ButtonElement { ActionId = "garbage" };
            ((IPayloadElement)button).BlockId = new InteractionCallbackInfo(nameof(Handler2));
            var payload = new ViewBlockActionsPayload
            {
                Actions = new List<IPayloadElement>
                {
                    button
                },
                View = new ModalView
                {
                    CallbackId = new InteractionCallbackInfo(nameof(Handler1)),
                }
            };
            var platformEvent = new PlatformEvent<IViewPayload>(
                payload,
                null,
                new BotChannelUser("B01234", "U01234", "abbot"),
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member(),
                null,
                new Organization());
            var handlers = new IHandler[] { new Handler1(), new Handler2(), new Handler3() };
            var registry = new HandlerRegistry(handlers);

            var handler = registry.Retrieve(platformEvent);

            Assert.IsType<Handler2>(handler);
        }

        [Fact]
        public void RetrievesHandlerForViewHandlerUsingActionId()
        {
            var button = new ButtonElement { ActionId = new InteractionCallbackInfo(nameof(Handler3)) };
            ((IPayloadElement)button).BlockId = new InteractionCallbackInfo(nameof(Handler2));
            var payload = new ViewBlockActionsPayload
            {
                Actions = new List<IPayloadElement>
                {
                    button
                },
                View = new ModalView
                {
                    CallbackId = new InteractionCallbackInfo(nameof(Handler1)),
                }
            };
            var platformEvent = new PlatformEvent<IViewPayload>(
                payload,
                null,
                new BotChannelUser("B01234", "U01234", "abbot"),
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member(),
                null,
                new Organization());
            var handlers = new IHandler[] { new Handler1(), new Handler2(), new Handler3() };
            var registry = new HandlerRegistry(handlers);

            var handler = registry.Retrieve(platformEvent);

            Assert.IsType<Handler3>(handler);
        }

        [Fact]
        public void RetrievesHandlerForViewHandlerUsingFirstActionIdWithValidCallbackId()
        {
            var randomButton = new ButtonElement { ActionId = "garbage" };
            var button = new ButtonElement { ActionId = new InteractionCallbackInfo(nameof(Handler3)) };
            var anotherButton = new ButtonElement { ActionId = new InteractionCallbackInfo(nameof(Handler3)) };
            ((IPayloadElement)button).BlockId = new InteractionCallbackInfo(nameof(Handler1));
            var payload = new ViewBlockActionsPayload
            {
                // As far as we know, there will only be one action in this payload, but
                // just in case, we want to make sure we do the right thing.
                Actions = new List<IPayloadElement>
                {
                    randomButton,
                    button,
                    anotherButton
                },
                View = new ModalView
                {
                    CallbackId = new InteractionCallbackInfo(nameof(Handler1)),
                }
            };
            var platformEvent = new PlatformEvent<IViewPayload>(
                payload,
                null,
                new BotChannelUser("B01234", "U01234", "abbot"),
                DateTimeOffset.UtcNow,
                new FakeResponder(),
                new Member(),
                null,
                new Organization());
            var handlers = new IHandler[] { new Handler1(), new Handler2(), new Handler3() };
            var registry = new HandlerRegistry(handlers);

            var handler = registry.Retrieve(platformEvent);

            Assert.IsType<Handler3>(handler);
        }
    }

    class Handler1 : IHandler
    {
        public Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
        {
            return Task.CompletedTask;
        }
    }

    class Handler2 : IHandler
    {
        public Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
        {
            return Task.CompletedTask;
        }
    }

    class Handler3 : IHandler
    {
        public Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
        {
            return Task.CompletedTask;
        }
    }
}
