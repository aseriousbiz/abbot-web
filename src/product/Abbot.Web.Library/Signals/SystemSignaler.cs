using Hangfire;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;

namespace Serious.Abbot.Signals;

/// <summary>
/// Used to raise a system signal.
/// </summary>
public class SystemSignaler : ISystemSignaler
{
    readonly IBackgroundJobClient _backgroundJobClient;

    public SystemSignaler(IBackgroundJobClient backgroundJobClient)
    {
        _backgroundJobClient = backgroundJobClient;
    }

    public void EnqueueSystemSignal(
        SystemSignal signal,
        string arguments,
        Id<Organization> organizationId,
        PlatformRoom room,
        Member actor,
        MessageInfo? triggeringMessage)
    {
        // Capture the host and scheme before we jump off the HttpContext and over to a background job.
        _backgroundJobClient.Enqueue<SignalHandler>(h => h.HandleSystemSignalAsync(
            signal,
            organizationId,
            arguments,
            room,
            actor,
            triggeringMessage));
    }
}
