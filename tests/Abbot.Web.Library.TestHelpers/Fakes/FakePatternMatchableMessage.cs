using Serious.Abbot.Messaging;

namespace Serious.TestHelpers;

public record FakePatternMatchableMessage(string Text) : IPatternMatchableMessage;
