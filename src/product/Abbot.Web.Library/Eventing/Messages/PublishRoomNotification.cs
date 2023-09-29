using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Entities;

namespace Serious.Abbot.Eventing.Messages;

/// <summary>
/// A notification from a room raised by a skill.
/// </summary>
public class PublishRoomNotification : IProvidesLoggerScope, IOrganizationMessage
{
    public required Id<Organization> OrganizationId { get; init; }
    public required Id<Room>? RoomId { get; init; }

    public required RoomNotification Notification { get; init; }

    static readonly Func<ILogger, Id<Organization>, Id<Room>?, IDisposable?> LoggerScope =
        LoggerMessage.DefineScope<Id<Organization>, Id<Room>?>(
            "PublishRoomNotification OrgId={OrganizationId} RoomId={RoomId}");

    public IDisposable? BeginScope(ILogger logger) =>
        LoggerScope(logger, OrganizationId, RoomId);
}
