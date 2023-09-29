using Microsoft.Bot.Schema;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.FeatureManagement;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Base class for events and messages coming from the chat platform. <see cref="MessageContext"/> represents both
/// messages and interactions with UI elements in a message. <see cref="ViewContext{T}"/> represents interactions within
/// a view such as with a modal dialog.
/// </summary>
public abstract record EventContext : IEventContext
{
    protected EventContext(IPlatformEvent platformEvent)
    {
        FromMember = platformEvent.From;
        Responder = platformEvent.Responder;
        Organization = platformEvent.Organization;
        Timestamp = platformEvent.Timestamp;
        Bot = platformEvent.Bot;
    }

    protected IResponder Responder { get; }

    /// <summary>
    /// Gets the current targeting context, used for feature gating.
    /// </summary>
    public TargetingContext GetTargetingContext() => Organization.CreateTargetingContext(FromMember.User.PlatformUserId);

    /// <summary>
    /// The user that initiated the event.
    /// </summary>
    public User From => FromMember.User;

    /// <summary>
    /// The date and time the event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// The member that initiated the event.
    /// </summary>
    public Member FromMember { get; }

    /// <summary>
    /// The organization the event occurred in.
    /// </summary>
    public Organization Organization { get; }

    /// <summary>
    /// Information about the bot user (aka Abbot).
    /// </summary>
    public BotChannelUser Bot { get; }

    /// <summary>
    /// Sends a message to the specified chat address.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="messageTarget">Where to send the message.</param>
    /// <returns></returns>
    public Task SendMessageAsync(IMessageActivity message, IMessageTarget messageTarget)
    {
        return Responder.SendActivityAsync(message, messageTarget);
    }
}
