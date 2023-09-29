using Serious.Abbot.Entities;

namespace Serious.Abbot.Eventing.Messages;

/// <summary>
/// Message sent when an <see cref="AnnouncementMessage"/> is sent to a <see cref="Room"/> for an
/// <see cref="Announcement"/>.
/// </summary>
/// <param name="Id">The database Id for the Announcement this message is for.</param>
public record AnnouncementMessageCompleted(Id<Announcement> Id) : ISessionFromEntity<Announcement>;
