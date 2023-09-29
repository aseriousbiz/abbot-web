using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

public record AttachConversationToHub : IProvidesLoggerScope, IOrganizationMessage
{
    public required Id<Hub> HubId { get; init; }
    public required Id<Organization> OrganizationId { get; init; }
    public required Id<Conversation> ConversationId { get; init; }
    public required Id<Member> ActorMemberId { get; init; }
    public Id<Organization> ActorOrganizationId { get; init; }

    static readonly Func<ILogger, int, int, int, int, int, IDisposable?> LoggerScope =
        LoggerMessage.DefineScope<int, int, int, int, int>("AttachConversationToHub HubId={HubId}, OrganizationId={OrganizationId}, ConversationId={ConversationId}, ActorMemberId={ActorMemberId}, ActorOrganizationId={ActorOrganizationId}");
    public IDisposable? BeginScope(ILogger logger) =>
        LoggerScope(logger, HubId, OrganizationId, ConversationId, ActorMemberId, ActorOrganizationId);
}
