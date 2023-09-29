using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks;

public record UpcomingPlaybookEvent
{
    public required UpcomingPlaybookEventType Type { get; init; }
    public required Playbook Playbook { get; init; }
    public required int Version { get; init; }
    public DateTime? ExpectedTime { get; init; }
    public PlaybookRun? PlaybookRun { get; init; }
}

public enum UpcomingPlaybookEventType
{
    Unknown,
    ScheduledDispatch,
    Resume,
}
