using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Provides context for the Abbot Conversation in which a skill is invoked (not to be confused with a Bot Framework
/// Conversation).
/// </summary>
/// <remarks>
/// This is a property of <see cref="SkillMessage" /> that is sent to skill runners in a serialized format.
/// </remarks>
/// <param name="Id">The database Id of the Conversation.</param>
/// <param name="FirstMessageId">The platform-specific message Id of the first message in the conversation.</param>
/// <param name="Title">The title of the conversation.</param>
/// <param name="WebUrl">The URL to the conversation details page.</param>
/// <param name="Room">The room the conversation is in.</param>
/// <param name="StartedBy">The user that started the conversation.</param>
/// <param name="Created">The date that the conversation was created.</param>
/// <param name="LastMessagePostedOn">The date of the most recent message in the conversation.</param>
/// <param name="Members">The members of the conversation.</param>
public record ChatConversation(
        string Id,
        string FirstMessageId,
        string Title,
        Uri WebUrl,
        IRoom Room,
        IChatUser StartedBy,
        DateTimeOffset Created,
        DateTimeOffset LastMessagePostedOn,
        IReadOnlyList<IChatUser> Members)
    : IConversation
{
    [JsonIgnore]
    public ChatAddress Address => new(ChatAddressType.Room, Room.Id, FirstMessageId);

    // The default equality defined by a record doesn't do SequenceEquals on lists :(.
    public virtual bool Equals(ChatConversation? other) =>
        other is not null && other.Id == Id && other.FirstMessageId == FirstMessageId && other.Title == Title &&
        Equals(other.WebUrl, WebUrl) && Equals(other.Room, Room) && Equals(other.StartedBy, StartedBy) &&
        Equals(other.Created, Created) && Equals(other.LastMessagePostedOn, LastMessagePostedOn) &&
        other.Members.SequenceEqual(Members);

    public override int GetHashCode() =>
        // We ran out of overload parameters!! So we have to recursively call HashCode.Combine now.
        HashCode.Combine(
            HashCode.Combine(Id, FirstMessageId, Title, WebUrl, Room, StartedBy, Created, LastMessagePostedOn),
            Members);
}
