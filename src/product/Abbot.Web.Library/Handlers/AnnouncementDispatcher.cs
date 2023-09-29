using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.PayloadHandlers;

/// <summary>
/// Class used to dispatch announcement logic to the appropriate target such as selected rooms, all rooms, or
/// customer segments.
/// </summary>
public class AnnouncementDispatcher
{
    readonly IEnumerable<IAnnouncementTarget> _targets;

    public AnnouncementDispatcher(IEnumerable<IAnnouncementTarget> targets)
    {
        _targets = targets;
    }

    /// <summary>
    /// Returns the <see cref="IAnnouncementTarget" /> that corresponds to the <paramref name="announcement" />.
    /// </summary>
    /// <param name="announcement">The announcement.</param>
    public IAnnouncementTarget GetAnnouncementTarget(Announcement announcement)
        => _targets.Single(t => t.IsTargetForAnnouncement(announcement));


    /// <summary>
    /// Returns the <see cref="IAnnouncementTarget" /> that corresponds to the <paramref name="targetName" />.
    /// </summary>
    /// <param name="targetName">The name of the target.</param>
    /// <returns></returns>
    public IAnnouncementTarget? GetAnnouncementTarget(string? targetName)
        => _targets.SingleOrDefault(t => t.IsSelectedTarget(targetName));
}

