using System.Collections.Generic;
using Microsoft.Bot.Builder;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Wraps a <see cref="ITurnContext" /> and represents an incoming message from Bot Framework, but translated
/// to the needs of Abbot. This context does not yet match users and organizations to the database.
/// </summary>
public interface IPlatformMessage : IPatternMatchableMessage, IPlatformEvent<MessageEventInfo>
{
    /// <summary>
    /// Platform specific message Id that uniquely identifies this message for the purposes of API calls
    /// to the platform.
    /// </summary>
    string? MessageId { get; }

    /// <summary>
    /// Platform specific thread Id that uniquely identifies the thread in which this message was posted. We
    /// expect this to be <c>null</c> for a top-level message.
    /// </summary>
    string? ThreadId { get; }

    /// <summary>
    /// If <c>true</c>, this message is a reply in a thread. If <c>false</c>, this message is a top-level message in a
    /// room.
    /// </summary>
    bool IsInThread { get; }

    /// <summary>
    /// The URL to the message.
    /// </summary>
    Uri? MessageUrl { get; }

    /// <summary>
    /// The mentioned users.
    /// </summary>
    IReadOnlyList<Member> Mentions { get; }

    /// <summary>
    /// Returns true if this is a direct message to the bot. Interactive events such as clicking on a button
    /// are considered direct messages as this indicates to Abbot that it shouldn't look for a bot mention.
    /// </summary>
    bool DirectMessage { get; }

    /// <summary>
    /// When sending a message, pass this as the <see cref="IMessageTarget" /> in order to reply in a thread instead
    /// of as the next message in a room.
    /// </summary>
    IMessageTarget? ReplyInThreadMessageTarget { get; }
}

public static class PlatformMessageExtensions
{
    /// <summary>
    /// Returns <c>true</c> if this message should be tracked as part of an existing conversation or as a new
    /// conversation.
    /// </summary>
    /// <remarks>
    /// Messages that are directed to Abbot (aka an attempt to call a skill) should not be tracked
    /// as part of a conversation except if the message is from a supportee AND the message is a pattern match.
    /// </remarks>
    /// <param name="platformMessage">The incoming message.</param>
    /// <param name="routeResult">The <see cref="RouteResult"/> for the current message.</param>
    public static bool ShouldTrackMessage(this IPlatformMessage platformMessage, RouteResult routeResult)
        => platformMessage.Organization.Enabled && !routeResult.IsDirectedAtBot;

    /// <summary>
    /// Returns <c>true</c> if this message should be handled by Abbot.
    /// </summary>
    /// <remarks>
    /// Just because Abbot should handle this message, it doesn't mean that the message shouldn't be tracked.
    /// If it's a pattern match, both things should happen.
    /// </remarks>
    /// <param name="platformMessage">The message.</param>
    /// <param name="routeResult">The <see cref="RouteResult"/> for the current message.</param>
    public static bool ShouldBeHandledByAbbot(this IPlatformMessage platformMessage, RouteResult routeResult)
    {
        return routeResult.IsDirectedAtBot || routeResult.IsPatternMatch || platformMessage.DirectMessage;
    }

    /// <summary>
    /// Returns <c>true</c> if this message is known to be from a supportee.
    /// </summary>
    /// <param name="platformMessage">The <see cref="IPlatformMessage"/> message.</param>
    /// <returns></returns>
    public static bool IsFromSupportee(this IPlatformMessage platformMessage)
    {
        return platformMessage.Room is { } room && ConversationTracker.IsSupportee(platformMessage.From, room);
    }

    /// <summary>
    /// Returns <c>true</c> if the message is from a user that is allowed to call a skill directly (aka by mentioning
    /// abbot, by DMing Abbot, or by using the skill shortcut). This does not apply to calling a skill in response
    /// to a pattern. This doesn't check permissions either.
    /// </summary>
    /// <param name="platformMessage">The <see cref="IPlatformMessage"/> message.</param>
    public static bool CanInvokeSkillDirectly(this IPlatformMessage platformMessage)
    {
        return !platformMessage.IsFromSupportee();
    }
}
