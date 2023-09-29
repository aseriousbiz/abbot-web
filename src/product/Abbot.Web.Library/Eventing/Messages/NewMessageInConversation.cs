using Microsoft.Extensions.Logging;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

public record NewMessageInConversation : IProvidesLoggerScope, ISessionFromConversation, IOrganizationMessage
{
    /// <summary>
    /// The database Id for the <see cref="Member"/> that sent this message.
    /// </summary>
    public required Id<Member> SenderId { get; init; }

    /// <summary>
    /// The database Id for the <see cref="Conversation"/> that this message is in.
    /// </summary>
    public required Id<Conversation> ConversationId { get; init; }

    /// <summary>
    /// The database Id for the <see cref="Room"/> that this message is in.
    /// </summary>
    public required Id<Room> RoomId { get; init; }

    /// <summary>
    /// The database Id for the <see cref="Organization"/> that this message is in.
    /// </summary>
    public required Id<Organization> OrganizationId { get; init; }

    /// <summary>
    /// The platform specific message id. In Slack, this is the <c>ts</c> value.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The platform specific thread id.
    /// </summary>
    public required string? ThreadId { get; init; }

    /// <summary>
    /// The message text.
    /// </summary>
    public required string MessageText { get; init; }

    /// <summary>
    /// The URL to the message in Slack.
    /// </summary>
    public required Uri MessageUrl { get; init; }

    /// <summary>
    /// Whether this message is a live message or imported.
    /// </summary>
    public required bool IsLive { get; set; }

    /// <summary>
    /// The current state of the conversation.
    /// </summary>
    public required ConversationState ConversationState { get; init; }

    /// <summary>
    /// The database Id for the <see cref="Hub"/> to which the conversation is attached, if any.
    /// </summary>
    public required Id<Hub>? HubId { get; init; }

    /// <summary>
    /// The result from classifying this message via the <see cref="MessageClassifier"/>.
    /// </summary>
    public ClassificationResult? ClassificationResult { get; init; }

    /// <summary>
    /// The ID of the Slack thread for this conversation in the Hub room associated with it, if any.
    /// </summary>
    public required string? HubThreadId { get; init; }

    static readonly Func<ILogger, int, int, int, string, IDisposable?> LoggerScope =
        LoggerMessage.DefineScope<int, int, int, string>(
            "NewMessageInConversation ConversationId={ConversationId} RoomId={RoomId} OrganizationId={OrganizationId} MessageId={MessageId}");
    public IDisposable? BeginScope(ILogger logger) =>
        LoggerScope(logger, ConversationId, RoomId, OrganizationId, MessageId);
}
