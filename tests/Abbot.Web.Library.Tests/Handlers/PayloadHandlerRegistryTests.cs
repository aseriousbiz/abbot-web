using Abbot.Common.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack.Payloads;
using Serious.TestHelpers;

public class PayloadHandlerRegistryTests
{
    public class TheRetrieveMethod
    {
        [Fact]
        public void RetrievesInvokerForTeamRenameEventPayload()
        {
            var organization = new Organization();
            var platformEvent = new FakePlatformEvent<TeamChangeEventPayload>(
                new TeamChangeEventPayload(),
                new Member(),
                organization);
            var services = new ServiceCollection();
            services.AddSingleton(Substitute.For<IBackgroundSlackClient>());
            services.RegisterAllHandlers();
            var serviceProvider = services.BuildServiceProvider();
            var registry = new PayloadHandlerRegistry(serviceProvider);

            var invoker = registry.Retrieve(platformEvent);

            Assert.IsType<PayloadHandlerInvoker<TeamChangeEventPayload>>(invoker);
        }

        [Fact]
        public void RetrievesInvokerForUserEventPayload()
        {
            var env = TestEnvironment.Create();
            var platformEvent = env.CreateFakePlatformEvent(new UserEventPayload("U", "T", "R", "D"));
            var registry = env.Activate<PayloadHandlerRegistry>();

            var invoker = registry.Retrieve(platformEvent);

            var typedInvoker = Assert.IsType<PayloadHandlerInvoker<UserEventPayload>>(invoker);
            Assert.IsType<UserPayloadHandler>(typedInvoker.PayloadHandler);
        }

        [Fact]
        public void RetrievesInvokerForViewClosedPayload()
        {
            var env = TestEnvironment.Create();
            var platformEvent = env.CreateFakePlatformEvent(new ViewClosedPayload());
            var registry = env.Activate<PayloadHandlerRegistry>();

            var invoker = registry.Retrieve(platformEvent);

            Assert.IsType<HandlerDispatcher>(invoker);
        }

        [Fact]
        public async Task RetrievesInvokerForPlatformMessage()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            var platformMessage = env.CreatePlatformMessage(room);
            var registry = env.Activate<PayloadHandlerRegistry>();

            var invoker = registry.Retrieve(platformMessage);

            Assert.IsType<HandlerDispatcher>(invoker);
        }
    }
}
