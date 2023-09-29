using System;
using System.Threading.Tasks;
using Microsoft.Bot.Schema;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Slack.BotFramework.Model;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Base class for events and messages coming from the chat platform. <see cref="MessageContext"/> represents both
/// messages and interactions with UI elements in a message. <see cref="IViewContext{T}"/> represents interactions within
/// a view such as with a modal dialog.
/// </summary>
public interface IEventContext : IFeatureActor
{
    /// <summary>
    /// The user sending the message.
    /// </summary>
    User From { get; }

    /// <summary>
    /// The date and time when this message was received. Not to be confused with the Slack timestamp which can be used to
    /// identify a Slack message.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// The membership status of the user sending the message.
    /// </summary>
    Member FromMember { get; }

    /// <summary>
    /// The organization for the chat platform.
    /// </summary>
    Organization Organization { get; }

    /// <summary>
    /// The Bot User.
    /// </summary>
    BotChannelUser Bot { get; }

    /// <summary>
    /// Sends a direct message to the current user.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="messageTarget">The target of the message.</param>
    Task SendMessageAsync(IMessageActivity message, IMessageTarget messageTarget);
}

public static class EventContextExtensions
{
    /// <summary>
    /// Sends a direct message to the current user.
    /// </summary>
    /// <param name="eventContext">The event context.</param>
    /// <param name="text">The text to send.</param>
    public static async Task SendDirectMessageAsync(this IEventContext eventContext, string text)
    {
        var activity = Activity.CreateMessageActivity();
        activity.Text = text;
        await eventContext.SendDirectMessageAsync(activity);
    }

    /// <summary>
    /// Sends a direct message to the current user.
    /// </summary>
    /// <param name="eventContext">The event context.</param>
    /// <param name="message">The message to send to the current user.</param>
    public static async Task SendDirectMessageAsync(this IEventContext eventContext, IMessageActivity message)
    {
        await eventContext.SendMessageAsync(message, new UserMessageTarget(eventContext.From.PlatformUserId));
    }

    /// <summary>
    /// Sends an ephemeral reply to the current user to the specified message.
    /// </summary>
    /// <param name="eventContext">The event context.</param>
    /// <param name="message">The message to send to the current user.</param>
    public static async Task SendEphemeralReplyAsync(this IEventContext eventContext, string message, string channel, string? messageId)
    {
        var replyChatAddress = new ChatAddress(
            ChatAddressType.Room,
            channel,
            messageId);

        var activity = new RichActivity(message)
        {
            EphemeralUser = eventContext.From.PlatformUserId,
        };
        await eventContext.SendMessageAsync(activity, new MessageTarget(replyChatAddress));
    }
}
