using System;

namespace Serious.Abbot.Entities;

/// <summary>
/// A record that links a <see cref="Serious.Abbot.Entities.Member"/> with a <see cref="Serious.Abbot.Entities.Conversation"/> they are participating in.
/// </summary>
public class ConversationMember
{
    /// <summary>
    /// The ID of this relationship
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The ID of the conversation this record relates to.
    /// </summary>
    public int ConversationId { get; set; }

    /// <summary>
    /// The <see cref="Serious.Abbot.Entities.Conversation"/> this record relates to.
    /// </summary>
    public Conversation Conversation { get; set; } = null!;

    /// <summary>
    /// The ID of the member this record relates to.
    /// </summary>
    public int MemberId { get; set; }

    /// <summary>
    /// The <see cref="Serious.Abbot.Entities.Member"/> this record relates to.
    /// </summary>
    public Member Member { get; set; } = null!;

    /// <summary>
    /// The time at which the user joined the conversation.
    /// </summary>
    public DateTime JoinedConversationAt { get; init; }

    /// <summary>
    /// The time at which the user posted their most recent message in this conversation.
    /// </summary>
    public DateTime LastPostedAt { get; set; }
}
