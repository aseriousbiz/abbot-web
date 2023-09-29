using System;
using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents an Abbot Managed Conversation.
/// </summary>
public interface IConversation : IMessageTarget
{
    /// <summary>
    /// The ID of the conversation.
    /// NOTE: Do not rely on the specific format of this value as it may change in the future.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The title of the conversation
    /// </summary>
    string Title { get; }

    /// <summary>
    /// The <see cref="IRoom"/> in which the conversation is taking place.
    /// </summary>
    IRoom Room { get; }

    /// <summary>
    /// The <see cref="IChatUser"/> who started this conversation.
    /// </summary>
    IChatUser StartedBy { get; }

    /// <summary>
    /// The time at which the first message was posted to this conversation.
    /// </summary>
    DateTimeOffset Created { get; }

    /// <summary>
    /// The time at which the last message was posted to this conversation.
    /// </summary>
    DateTimeOffset LastMessagePostedOn { get; }

    /// <summary>
    /// A list of <see cref="IChatUser"/> objects representing the users who are participating in the conversation
    /// </summary>
    IReadOnlyList<IChatUser> Members { get; }

    /// <summary>
    /// Gets the URL for the view of this conversation on Abbot's web interface.
    /// Only users of the workspace in which this conversation is taking place will be able to view this link,
    /// but it is safe to show it to non-workspace members.
    /// </summary>
    Uri WebUrl { get; }
}
