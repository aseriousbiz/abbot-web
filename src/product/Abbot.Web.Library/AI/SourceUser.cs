using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;

namespace Serious.Abbot.AI;

/// <summary>
/// The source user.
/// </summary>
/// <param name="Id">The slack user id.</param>
/// <param name="Role">The role of the user, either "Customer" or "Support".</param>
public record SourceUser(string Id, string Role)
{
    /// <summary>
    /// Creates a <see cref="SourceUser"/> from a <see cref="Member"/> and a <see cref="Room"/>.
    /// </summary>
    /// <param name="member">The <see cref="Member"/>.</param>
    /// <param name="room">The <see cref="Room"/>.</param>
    /// <returns>A <see cref="SourceUser"/>.</returns>
    public static SourceUser FromMemberAndRoom(Member member, Room room)
        => new(member.User.PlatformUserId, ConversationTracker.IsSupportee(member, room) ? "Customer" : "Support Agent");

    /// <summary>
    /// Creates a <see cref="SourceUser"/> from a <see cref="MessagePostedEvent"/>.
    /// </summary>
    /// <param name="messagePostedEvent">The message posted event.</param>
    /// <returns>A <see cref="SourceUser"/>.</returns>
    public static SourceUser FromMessagePostedEvent(MessagePostedEvent messagePostedEvent)
        => FromMemberAndRoom(messagePostedEvent.Member, messagePostedEvent.Conversation.Room);

    /// <summary>
    /// Formats the user for use in a participation list or at the beginning of a message to indicate who the
    /// message is from.
    /// </summary>
    /// <returns>The format.</returns>
    public string Format() => $"<@{Id}> ({Role})";
}
