using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;

namespace Serious.TestHelpers;

public record FakePlatformEvent<T>(T Payload, Member From, Organization Organization) : PlatformEvent<T>(
    Payload,
    null,
    BotChannelUser.GetBotUser(Organization),
    DateTimeOffset.UtcNow,
    new FakeResponder(),
    From,
    null,
    Organization)
{
    public new FakeResponder Responder => (FakeResponder)base.Responder;
}
