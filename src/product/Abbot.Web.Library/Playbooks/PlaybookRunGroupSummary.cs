using System.Linq;
using System.Linq.Expressions;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Playbooks;

public record PlaybookRunGroupSummary
{
    /// <summary>
    /// Gets or inits the <see cref="PlaybookRunGroup"/> referred to by this summary.
    /// </summary>
    public required PlaybookRunGroup Group { get; init; }

    /// <summary>
    /// Gets or inits the latest, or only, run in the group, by completion time, or create time if not completed.
    /// </summary>
    public PlaybookRun? LatestRun { get; init; }

    /// <summary>
    /// Gets or inits the number of runs in the group.
    /// </summary>
    public int RunCount { get; init; }
}

public static class PlaybookRunGroupSummaryQueryableExtensions
{
    public static IQueryable<PlaybookRunGroupSummary> SelectRunGroupSummary(this IQueryable<PlaybookRunGroup> query) =>
        query.Select(g => new PlaybookRunGroupSummary()
        {
            Group = g,
            LatestRun = g.Runs.OrderByDescending(r => r.CompletedAt ?? r.StartedAt ?? r.Created).FirstOrDefault(),
            RunCount = g.Runs.Count(),
        });
}
