using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Messages;
using Serious.EntityFrameworkCore;
using Serious.Filters;
using Serious.Slack;

namespace Serious.Abbot.Entities;

[DebuggerDisplay("{Name} ({PlatformRoomId}, Id: {Id})")]
public class Room : OrganizationEntityBase<Room>, IFilterableEntity<Room, AbbotContext>
{
    public Room()
    {
        Metadata = new EntityList<RoomMetadataField>();
    }

    // Special constructor called by EF Core.
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once UnusedMember.Local
    Room(DbContext db)
    {
        Metadata = new EntityList<RoomMetadataField>(db, this, nameof(Metadata));
    }

    /// <summary>
    /// The name of the room.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The <see cref="Entities.Customer"/> this room belongs to.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// The Id of the <see cref="Entities.Customer"/> this room belongs to.
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// The platform-specific ID of the room.
    /// </summary>
    public string PlatformRoomId { get; init; } = null!;

    /// <summary>
    /// Indicates if Abbot Managed Conversations are enabled for this room.
    /// </summary>
    public bool ManagedConversationsEnabled { get; set; }

    /// <summary>
    /// The date that conversation management was enabled for this room.
    /// </summary>
    public DateTime? DateManagedConversationsEnabledUtc { get; set; }

    /// <summary>
    /// Whether or not the room is a persistent room. In Slack, this would be a normal channel as opposed to
    /// a DM or a group DM.
    /// </summary>
    /// <remarks>
    /// Whether or not this conversation can be attached to with a trigger. Attachable conversations occur in
    /// rooms or channels that have a name and are not a DM or group DM.
    /// </remarks>
    public bool Persistent { get; set; }

    /// <summary>
    /// Whether or not the room is externally shared with a remote organization (or pending approval to be shared) or
    /// shared with another workspace in the same Enterprise Grid organization.
    /// </summary>
    /// <remarks>
    /// From a Slack API perspective, this is <c>true</c> if <c>is_pending_ext_shared</c> is <c>true</c> or
    /// <c>is_shared</c> is <c>true</c>. And according to Slack, <c>is_shared</c> is <c>true</c> if
    /// <c>is_externally_shared</c> or <c>is_org_shared</c> is <c>true</c>.
    /// </remarks>
    public bool? Shared { get; set; }

    /// <summary>
    /// Gets the type of the room.
    /// </summary>
    [Column(TypeName = "text")]
    public RoomType RoomType { get; set; } = RoomType.Unknown;

    /// <summary>
    /// Gets or sets a boolean indicating if the room has been deleted in the chat platform.
    /// </summary>
    public bool? Deleted { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating if a room has been archived in the chat platform.
    /// This does not mean Abbot is no longer a member of the channel.
    /// </summary>
    public bool? Archived { get; set; }

    /// <summary>
    /// Gets or sets a boolean indicating if Abbot is a member of the room.
    /// </summary>
    public bool? BotIsMember { get; set; }

    /// <summary>
    /// The UTC date and time of the last modification to this object.
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// A list of <see cref="RoomAssignment"/>s describing the users assigned to this room for various purposes.
    /// </summary>
    public IList<RoomAssignment> Assignments { get; set; } = null!;

    /// <summary>
    /// The conversations that have taken place in this room.
    /// </summary>
    public IList<Conversation> Conversations { get; init; } = null!;

    /// <summary>
    /// A <see cref="Threshold{TimeSpan}"/> that defines the maximum time between when a non-organization member posts and when an organization member responds.
    /// </summary>
    public Threshold<TimeSpan> TimeToRespond { get; set; } = new(null, null);

    /// <summary>
    /// Gets or sets a <see cref="RoomSettings"/> that contains the actual settings.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public RoomSettings? Settings { get; set; }

    /// <summary>
    /// Gets the UTC timestamp of the last platform update for this entity.
    /// </summary>
    public DateTime? LastPlatformUpdate { get; set; }

    /// <summary>
    /// A list of <see cref="RoomLink"/> objects representing links to external resources within the room.
    /// </summary>
    public IList<RoomLink> Links { get; set; } = new List<RoomLink>();

    /// <summary>
    /// Retrieves the custom metadata for the room.
    /// </summary>
    public EntityList<RoomMetadataField> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the ID of the Primary <see cref="Hub"/> for this <see cref="Room"/>.
    /// </summary>
    public int? HubId { get; set; }

    /// <summary>
    /// Gets or sets the Primary <see cref="Hub"/> for this <see cref="Room"/>.
    /// </summary>
    public Hub? Hub { get; set; }

    /// <summary>
    /// Last message activity in the room. This would be any non-bot message posted in the room or interaction
    /// with a message (such as message buttons).
    /// </summary>
    public DateTime? LastMessageActivityUtc { get; set; }

    /// <summary>
    /// Returns a string with the Slack room mention syntax for the room.
    /// </summary>
    /// <remarks>
    /// For now, we just use Slack as that's where our priorities lie.
    /// </remarks>
    /// <param name="useLinkForPrivateRoom">If the room is private, use a link to the room instead of a room mention</param>
    /// <returns>The Slack formatted room mention.</returns>
    public string ToMention(bool useLinkForPrivateRoom = false)
    {
        return !useLinkForPrivateRoom || RoomType is RoomType.PublicChannel
            ? $"<#{PlatformRoomId}>"
            : new Hyperlink(SlackFormatter.RoomUrl(Organization.Domain, PlatformRoomId), $"#{Name ?? "Unknown Room"}").ToString();
    }

    /// <inheritdoc cref="IFilterableEntity{TEntity, TContext}.GetFilterItemQueries"/>
    public static IEnumerable<IFilterItemQuery<Room>> GetFilterItemQueries(AbbotContext dbContext)
        => RoomFilters.CreateFilters(dbContext);
}

public enum RoomType
{
    /// <summary>
    /// The type of the room is unknown.
    /// </summary>
    Unknown,

    /// <summary>
    /// The room is a channel that any user in the organization can join.
    /// </summary>
    PublicChannel,

    /// <summary>
    /// The room is a channel that a user must be explicitly invited to join.
    /// </summary>
    PrivateChannel,

    /// <summary>
    /// The room is a direct message session with a single user.
    /// </summary>
    DirectMessage,

    /// <summary>
    /// The room is a direct message session with multiple users.
    /// </summary>
    MultiPartyDirectMessage,
}

public static class RoomExtensions
{
    public static bool IsActive(this Room room)
    {
        return room.Archived != true && room.Deleted != true && room.BotIsMember != false;
    }

    public static string SupporteeType(this Room room) => room switch
    {
        { Shared: true } => "external users",
        { Settings.IsCommunityRoom: true } => "community members (non agents)",
        _ => "guest users",
    };

    /// <summary>
    /// Gets a boolean indicating if the specified <see cref="RoomType"/> is considered "persistent".
    /// </summary>
    public static bool IsPersistent(this RoomType type) => type switch
    {
        RoomType.PrivateChannel or RoomType.PublicChannel => true,
        _ => false,
    };

    /// <summary>
    /// Gets a boolean indicating if the room needs to be updated with information from the platform.
    /// This helps ensure that even if we miss webhooks, we don't end up with stale data.
    /// A room is considered "out of date" if it is missing newly-added date that needs to be fetched from the platform.
    /// </summary>
    /// <param name="room">The <see cref="Room"/> to be updated.</param>
    // TODO: In the future we could use LastPlatformUpdate to queue a background job occasionally to refresh room metadata.
    public static bool NeedsPlatformUpdate(this Room room) =>
        room.RoomType == RoomType.Unknown ||
        room.Deleted == null ||
        room.Archived == null ||
        room.BotIsMember == null ||
        room.Shared == null;

    /// <summary>
    /// Gets a platform-specific link to the rooms
    /// </summary>
    /// <param name="room">The <see cref="Room"/> to get the link for.</param>
    /// <returns>A URL that will open the room.</returns>
    public static Uri GetLaunchUrl(this Room room) =>
        SlackFormatter.RoomUrl(room.Organization.Domain,
            room.PlatformRoomId);

    /// <summary>
    /// Returns a new <see cref="PlatformRoom"/> instance with the same properties as this <see cref="Room"/>.
    /// </summary>
    /// <param name="room">The <see cref="Room"/>.</param>
    /// <returns>A <see cref="PlatformRoom"/>.</returns>
    public static PlatformRoom ToPlatformRoom(this Room room)
    {
        return new PlatformRoom(room.PlatformRoomId, room.Name);
    }

    public static bool HasCustomResponseTimes(this Room room)
    {
        return room.TimeToRespond.Warning is not null || room.TimeToRespond.Deadline is not null;
    }
}
