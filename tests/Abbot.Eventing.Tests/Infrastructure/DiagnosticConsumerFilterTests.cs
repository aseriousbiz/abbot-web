using MassTransit.SignalR.Consumers;
using MassTransit.SignalR.Contracts;
using Serious.Abbot.Live;

// Can't use a file-scoped namespace because we have another namespace in the same file.
namespace Serious.Abbot.Eventing.Infrastructure
{
    public class DiagnosticConsumerFilterTests
    {
        public class TheGetFriendlyNameMethod
        {
            [Theory]
            [InlineData(typeof(SystemNotificationsConsumer), "SystemNotificationsConsumer")]
            [InlineData(typeof(GroupConsumer<FlashHub>), "SignalR:GroupConsumer<FlashHub>")]
            [InlineData(typeof(Group<FlashHub>), "SignalR:Group<FlashHub>")]
            [InlineData(typeof(InlineDataAttribute), "Xunit.InlineDataAttribute")]
            [InlineData(
                typeof(MassTransit.DynamicInternal.MassTransit.SignalR.Contracts.Group<FlashHub>),
                "SignalR:Group<FlashHub>")]
            public void ReturnsFriendlyNameForType(Type t, string expected)
            {
                Assert.Equal(expected, DiagnosticConsumerConsumeFilter<object, object>.GetFriendlyName(t));
            }
        }
    }
}

namespace MassTransit.DynamicInternal.MassTransit.SignalR.Contracts
{
    // Just a silly way to get the type name we want to verify.
    // Normally this type is dynamically generated, so we just have to write a class with this name for the test :)
    public record Group<T>;
}
