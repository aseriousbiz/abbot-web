using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messages;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;

namespace Serious.Abbot.Eventing.Messages;

// This file defines new _data-only_ records that act as the "new versions" of IPlatformEvent, IPlatformMessage, and MessageContext
// MassTransit consumers cannot use these legacy interfaces, so we need to create new records.
// As we shift more work to consumers, we want to move away from "combo" types like IPlatformEvent, MessageContext, and IPlatformMessage
// which encode both data and behavior.
//
// Remember: Composition over Inheritance!
// These are Pure Data Objects, so a ChatMessage _contains_ a ChatEvent, instead of inheriting.
// If you want the "benefits" of inheritance, add forwarder properties to ChatMessage!

/// <summary>
/// Metadata describing an event received from a chat platform.
/// </summary>
public record ChatEvent
{
    /// <summary>
    /// The ID of the <see cref="Organization"/> in which the message was received.
    /// </summary>
    public required Id<Organization> OrganizationId { get; init; }

    /// <summary>
    /// The ID of the <see cref="Member"/> that initiated the event or sent the message
    /// </summary>
    public required Id<Member> SenderId { get; init; }

    /// <summary>
    /// The ID of the <see cref="Room"/> in which the event occurred, if any.
    /// </summary>
    public Id<Room>? RoomId { get; init; }

    /// <summary>
    /// The UTC time when the message was received.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// A short-lived ID that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see> in Slack.
    /// </summary>
    public string? TriggerId { get; init; }

    // This is the bridge between the IPlatformMessage/MessageContext objects that we use in existing code,
    // and the new ChatMessage/ChatEvent objects that we're using in new MassTransit Consumers
    /// <summary>
    /// Creates a new <see cref="ChatEvent"/> from the given <see cref="IPlatformEvent"/>.
    /// </summary>
    /// <param name="evt">The <see cref="IPlatformEvent"/> representing the event.</param>
    public static ChatEvent Create(IPlatformEvent evt)
    {
        return new ChatEvent
        {
            OrganizationId = evt.Organization,
            SenderId = evt.From,
            RoomId = evt.Room,
            Timestamp = evt.Timestamp,
            TriggerId = evt.TriggerId,
        };
    }
}

/// <summary>
/// Metadata describing a message received from a chat platform.
/// </summary>
public record ChatMessage
{
    /// <summary>
    /// A <see cref="ChatEvent"/> describing the common event metadata associated with this message.
    /// </summary>
    public required ChatEvent Event { get; init; }

    /// <summary>
    /// The ID of the <see cref="Conversation"/> in which the message was received.
    /// </summary>
    public Id<Conversation>? ConversationId { get; init; }

    /// <summary>
    /// The platform-specific ID of the message.
    /// </summary>
    public required string MessageId { get; init; }

    /// <summary>
    /// The platform-specific ID of the parent thread in which the message was received.
    /// </summary>
    public string? ThreadId { get; init; }

    /// <summary>
    /// The original text of the message.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// A collection of <see cref="Member"/> IDs describing the users mentioned in the message.
    /// </summary>
    public required IReadOnlyList<Id<Member>> MentionedUsers { get; init; }

    // This is the bridge between the IPlatformMessage/MessageContext objects that we use in existing code,
    // and the new ChatMessage/ChatEvent objects that we're using in new MassTransit Consumers
    /// <summary>
    /// Creates a new <see cref="ChatMessage"/> from the given <see cref="IPlatformMessage"/> and <see cref="Conversation"/>.
    /// </summary>
    /// <remarks>
    /// (Conversation is not available on <see cref="IPlatformMessage"/>)
    /// </remarks>
    /// <param name="message">The <see cref="IPlatformMessage"/> representing the message.</param>
    /// <param name="conversation">The <see cref="Conversation"/> associated with the message, if any.</param>
    public static ChatMessage Create(IPlatformMessage message, Conversation? conversation)
    {
        return new()
        {
            Event = ChatEvent.Create(message),
            ConversationId = conversation,
            MessageId = message.MessageId.Require(),
            ThreadId = message.ThreadId,
            Text = message.Text,
            MentionedUsers = message.Mentions.ToIds(),
        };
    }
}
