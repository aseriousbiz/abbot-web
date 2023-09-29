using System.Collections.Generic;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Live;
using Serious.Logging;

namespace Serious.Abbot.Conversations;

/// <summary>
/// Publishes messages related to <see cref="Conversation"/>.
/// </summary>
public interface IConversationPublisher
{
    /// <summary>
    /// Publishes a <see cref="NewConversation"/> message when a new conversation is created.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> that was created.</param>
    /// <param name="message">The <see cref="ConversationMessage"/> with info about the message.</param>
    /// <param name="messagePostedEvent">The <see cref="MessagePostedEvent"/> for the message that created the conversation.</param>
    Task PublishNewConversationAsync(
        Conversation conversation,
        ConversationMessage message,
        MessagePostedEvent messagePostedEvent);

    /// <summary>
    /// Publishes a <see cref="NewMessageInConversation"/> when a new message is posted in a conversation.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/> that received a new message.</param>
    /// <param name="message">The <see cref="ConversationMessage"/> with info about the message.</param>
    /// <param name="messagePostedEvent">The <see cref="MessagePostedEvent"/> for the new message in the conversation.</param>
    Task PublishNewMessageInConversationAsync(
        Conversation conversation,
        ConversationMessage message,
        MessagePostedEvent messagePostedEvent);

    /// <summary>
    /// Publishes a <see cref="ConversationStateChanged"/> message when a conversation's state changes.
    /// </summary>
    /// <param name="stateChangedEvent">Information about the state change.</param>
    Task PublishConversationStateChangedAsync(StateChangedEvent stateChangedEvent);
}

public class ConversationPublisher : IConversationPublisher
{
    static readonly ILogger<ConversationPublisher> Log =
        ApplicationLoggerFactory.CreateLogger<ConversationPublisher>();

    readonly IEnumerable<IConversationListener> _listeners;
    readonly IPublishEndpoint _publishEndpoint;
    readonly IFlashPublisher _flashPublisher;

    public ConversationPublisher(
        IEnumerable<IConversationListener> listeners,
        IPublishEndpoint publishEndpoint,
        IFlashPublisher flashPublisher)
    {
        _listeners = listeners;
        _publishEndpoint = publishEndpoint;
        _flashPublisher = flashPublisher;
    }

    public async Task PublishNewConversationAsync(
        Conversation conversation,
        ConversationMessage message,
        MessagePostedEvent messagePostedEvent)
    {
        await InvokeListenersAsync(l => l.OnNewConversationAsync, conversation, message);

        // First we publish that a new conversation has been created
        await _publishEndpoint.Publish(new NewConversation(
            conversation,
            conversation.Organization,
            (Id<Hub>?)message.Room.HubId ?? message.Organization.Settings.DefaultHubId,
            messagePostedEvent.MessageUrl ?? message.GetMessageUrl()));

        // Notify the UI that the conversation list has changed
        await _flashPublisher.PublishAsync(
            FlashName.ConversationListUpdated,
            FlashGroup.Organization(message.Organization));

        // Then we publish the message, but without calling the listeners again.
        // The reason we don't call it again is the IConversationListener contract (and implementations)
        // assume that OnNewMessageAsync is not called for the first message.
        await PublishConversationMessageAsync(conversation, message, messagePostedEvent);
    }

    public async Task PublishNewMessageInConversationAsync(
        Conversation conversation,
        ConversationMessage message,
        MessagePostedEvent messagePostedEvent)
    {
        // Call the listeners. This primarily updates integrations such as Zendesk and HubSpot.
        await InvokeListenersAsync(l => l.OnNewMessageAsync, conversation, message);

        // Update UI. This method contract is such that it won't throw.
        await _flashPublisher.PublishAsync(
            FlashName.ConversationListUpdated,
            FlashGroup.Organization(message.Organization));

        // Publish a message to the bus.
        // We will eventually migrate all the listeners to use the bus, but for now we do both.
        await PublishConversationMessageAsync(conversation, message, messagePostedEvent);
    }

    async Task PublishConversationMessageAsync(
        Conversation conversation,
        ConversationMessage message,
        MessagePostedEvent messagePostedEvent)
    {
        await _publishEndpoint.Publish(new NewMessageInConversation
        {
            SenderId = message.From,
            ConversationId = conversation,
            RoomId = new(conversation.RoomId),
            OrganizationId = new(conversation.OrganizationId),
            MessageId = message.MessageId,
            ThreadId = message.ThreadId,
            MessageText = message.Text,
            IsLive = message.IsLive,
            MessageUrl = messagePostedEvent.MessageUrl ?? message.GetMessageUrl(),
            // Include some extra fields about the conversation to allow listeners to make quick decisions about whether to process the message
            // without having to go to the DB.
            ConversationState = conversation.State,
            HubId = (Id<Hub>?)conversation.HubId,
            HubThreadId = conversation.HubThreadId,
            ClassificationResult = message.ClassificationResult,
        });
    }

    public async Task PublishConversationStateChangedAsync(StateChangedEvent stateChangedEvent)
    {
        using var _ = Log.BeginConversationEventScopes(stateChangedEvent);
        Log.StateChanged(stateChangedEvent.OldState, stateChangedEvent.NewState, stateChangedEvent.Implicit);

        await InvokeListenersAsync(l => l.OnStateChangedAsync, stateChangedEvent);

        // Notify the UI that the conversation list has changed
        await _flashPublisher.PublishAsync(
            FlashName.ConversationListUpdated,
            FlashGroup.Organization(stateChangedEvent.Conversation));

        await _publishEndpoint.Publish(new ConversationStateChanged
        {
            Conversation = stateChangedEvent.Conversation,
            Actor = stateChangedEvent.Member,
            Timestamp = stateChangedEvent.Created,
            OldState = stateChangedEvent.OldState,
            NewState = stateChangedEvent.NewState,
            Implicit = stateChangedEvent.Implicit,
            MessageId = stateChangedEvent.MessageId,
            ThreadId = stateChangedEvent.ThreadId,
            MessageUrl = stateChangedEvent.MessageUrl,
        });
    }

    async Task InvokeListenersAsync<T1>(
        Func<IConversationListener, Func<T1, Task>> funcSelector,
        T1 p1)
    {
        foreach (var listener in _listeners)
        {
            var func = funcSelector(listener);
            try
            {
                await func(p1);
            }
            // ConversationListeners should be passive.
            // An error in a listener should not prevent further processing of the message.
            catch (Exception ex)
            {
                Log.ExceptionUnhandledByListener(ex, listener.GetType().FullName, func.Method.Name);
            }
        }
    }

    async Task InvokeListenersAsync<T1, T2>(
        Func<IConversationListener, Func<T1, T2, Task>> funcSelector,
        T1 p1, T2 p2)
    {
        foreach (var listener in _listeners)
        {
            var func = funcSelector(listener);
            try
            {
                await func(p1, p2);
            }
            // ConversationListeners should be passive.
            // An error in a listener should not prevent further processing of the message.
            catch (Exception ex)
            {
                Log.ExceptionUnhandledByListener(ex, listener.GetType().FullName, func.Method.Name);
            }
        }
    }
}

static partial class ConversationPublisherLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "State changed from {OldConversationState} to {NewConversationState}. (Implicit={ImplicitConversationStateChange})")]
    public static partial void StateChanged(
        this ILogger<ConversationPublisher> logger,
        ConversationState oldConversationState, ConversationState newConversationState, bool implicitConversationStateChange);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Critical,
        Message = "An unhandled exception occurred while processing {ConversationListenerType}.{ConversationListenerMethod}.")]
    public static partial void ExceptionUnhandledByListener(
        this ILogger<ConversationPublisher> logger,
        Exception ex,
        string? conversationListenerType,
        string conversationListenerMethod);
}
