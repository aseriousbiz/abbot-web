using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Logging;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles room events and updates information about the room in response.
/// </summary>
public class RoomPayloadHandler : IPayloadHandler<RoomEventPayload>
{
    static readonly ILogger<RoomPayloadHandler> Log = ApplicationLoggerFactory.CreateLogger<RoomPayloadHandler>();

    readonly ISlackResolver _slackResolver;

    public RoomPayloadHandler(ISlackResolver slackResolver)
    {
        _slackResolver = slackResolver;
    }

    /// <summary>
    /// When receiving a room platform event, causes the room information to be updated based on an API call
    /// to <c>conversations.info</c> rather than updating the room directly from the event information.
    /// </summary>
    /// <param name="platformEvent">The incoming platform event.</param>
    public async Task OnPlatformEventAsync(IPlatformEvent<RoomEventPayload> platformEvent)
    {
        Log.MethodEntered(typeof(RoomPayloadHandler), nameof(OnPlatformEventAsync), "Room Event");

        await _slackResolver.ResolveRoomAsync(
            platformEvent.Payload.PlatformRoomId,
            platformEvent.Organization,
            forceRefresh: true);
    }
}
