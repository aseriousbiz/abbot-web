using Microsoft.Extensions.Logging;
using Serious.Abbot.Clients;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Logging;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Handles team rename events.
/// </summary>
public class TeamChangePayloadHandler : IPayloadHandler<TeamChangeEventPayload>
{
    static readonly ILogger<TeamChangePayloadHandler> Log = ApplicationLoggerFactory.CreateLogger<TeamChangePayloadHandler>();

    readonly IBackgroundSlackClient _backgroundSlackClient;

    public TeamChangePayloadHandler(IBackgroundSlackClient backgroundSlackClient)
    {
        _backgroundSlackClient = backgroundSlackClient;
    }

    public async Task OnPlatformEventAsync(IPlatformEvent<TeamChangeEventPayload> platformEvent)
    {
        Log.MethodEntered(typeof(TeamChangePayloadHandler), nameof(OnPlatformEventAsync), "Team Change activity");

        Expect.True(platformEvent.Payload.TeamId == platformEvent.Organization.PlatformId,
            $"Got a team change event for a team {platformEvent.Payload.TeamId} that is not the current team {platformEvent.Organization.PlatformId}.");

        _backgroundSlackClient.EnqueueUpdateOrganization(platformEvent.Organization);
    }
}
