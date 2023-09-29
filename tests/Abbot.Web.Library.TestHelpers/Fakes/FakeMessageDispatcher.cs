using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services;

namespace Serious.TestHelpers;

public class FakeMessageDispatcherWrapper : IMessageDispatcher
{
    public string? MessageIdToReturn { get; set; }

    public bool Success { get; set; } = true;

    public Task<ProactiveBotMessageResponse> DispatchAsync(BotMessageRequest message, Organization organization)
    {
        DispatchedMessage = new DispatchedMessage(message, organization);
        return Task.FromResult(new ProactiveBotMessageResponse(Success, MessageId: MessageIdToReturn));
    }

    public DispatchedMessage? DispatchedMessage { get; private set; }
}

public record DispatchedMessage(BotMessageRequest Message, Organization Organization);
