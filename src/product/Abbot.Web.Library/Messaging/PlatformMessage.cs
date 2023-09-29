using System.Collections.Generic;
using System.Linq;
using Microsoft.Bot.Builder;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Wraps a <see cref="ITurnContext" /> and represents an incoming message from Bot Framework, but translated
/// to the needs of Abbot. This context does not yet match users and organizations to the database.
/// </summary>
public class PlatformMessage : IPlatformMessage
{
    /// <summary>
    /// Constructor for a <see cref="PlatformMessage" />.
    /// </summary>
    /// <param name="messageEventInfo">Information about the incoming message event.</param>
    /// <param name="organization">The organization this message originated in.</param>
    /// <param name="timestamp">The date the message was created.</param>
    /// <param name="responder">Used to respond to chat.</param>
    /// <param name="from">The user sending the message.</param>
    /// <param name="botUser">The bot user. This should be Abbot.</param>
    /// <param name="mentions">The mentioned users, not including Abbot.</param>
    /// <param name="messageUrl">The Url to the message.</param>
    /// <param name="room">The <see cref="Room"/> entity if the room is a persistent room.</param>
    public PlatformMessage(
        MessageEventInfo messageEventInfo,
        Uri? messageUrl,
        Organization organization,
        DateTimeOffset timestamp,
        IResponder responder,
        Member from,
        BotChannelUser botUser,
        IEnumerable<Member> mentions,
        Room? room)
    {
        Text = messageEventInfo.Text;
        Organization = organization;
        PlatformId = organization.PlatformId;
        From = from;
        Mentions = mentions.ToList();
        DirectMessage = messageEventInfo.DirectMessage;
        Payload = messageEventInfo;
        Responder = responder;

        Bot = botUser;
        MessageUrl = messageUrl;
        MessageId = messageEventInfo.MessageId is { Length: > 0 } ts ? ts : null;
        Timestamp = timestamp;
        Room = room;

        TriggerId = messageEventInfo.InteractionInfo?.TriggerId;
        ThreadId = Payload.ThreadId is { Length: > 0 } threadId ? threadId : null;
        var replyThreadId = ThreadId ?? MessageId;
        ReplyInThreadMessageTarget = Room is not null && replyThreadId is not null
            ? new ReplyInThreadMessageTarget(Room.PlatformRoomId, replyThreadId)
            : null;
    }

    public string? MessageId { get; }

    public string? ThreadId { get; }

    public bool IsInThread => ThreadId is { Length: > 0 };

    public Uri? MessageUrl { get; }

    public string Text { get; }

    public string PlatformId { get; }

    public Organization Organization { get; }

    public Member From { get; }

    public IReadOnlyList<Member> Mentions { get; }

    public MessageEventInfo Payload { get; }

    public bool DirectMessage { get; }

    public IMessageTarget? ReplyInThreadMessageTarget { get; }

    public string? TriggerId { get; }

    public IResponder Responder { get; }

    object IPlatformEvent.Payload => Payload;

    public BotChannelUser Bot { get; }

    public DateTimeOffset Timestamp { get; }

    public Room? Room { get; }

    public TargetingContext GetTargetingContext() => Organization.CreateTargetingContext(From.User.PlatformUserId);
}
