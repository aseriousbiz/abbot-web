using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

/// <summary>
/// Event published when a new conversation is started.
/// </summary>
/// <param name="ConversationId">The database Id for the new <see cref="Conversation"/>.</param>
/// <param name="OrganizationId">The database Id for the <see cref="Organization"/> that conversation belongs to.</param>
/// <param name="RoomHubId">The database Id for the <see cref="Hub"/> that the <see cref="Room"/> in which this conversation was started is attached to.</param>
/// <param name="MessageUrl">The URL to the message that started this conversation.</param>
public record NewConversation(
    Id<Conversation> ConversationId,
    Id<Organization> OrganizationId,
    Id<Hub>? RoomHubId,
    Uri MessageUrl) : IProvidesLoggerScope, IOrganizationMessage
{
    static readonly Func<ILogger, int, IDisposable?> LoggerScope =
        LoggerMessage.DefineScope<int>($"{nameof(NewConversation)} ConversationId={{ConversationId}}");

    public IDisposable? BeginScope(ILogger logger) =>
        LoggerScope(logger, ConversationId);
}
