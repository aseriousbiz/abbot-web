using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Metadata;
using Serious.Abbot.Models;
using Serious.Abbot.Skills;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Events;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Represents an incoming message coming from a chat platform via the Bot Framework. Most likely this message
/// comes from the Azure Bot Service or the Direct Line API.
/// </summary>
public record MessageContext : EventContext
{
    static readonly ILogger<MessageContext> Log = ApplicationLoggerFactory.CreateLogger<MessageContext>();

    public MessageContext(
        IPlatformMessage platformMessage,
        string skillName,
        string arguments,
        string commandText,
        string originalMessage,
        string sigil,
        IReadOnlyList<SkillPattern> patterns,
        SkillDataScope? scope
        ) : base(platformMessage)
    {
        var messageEventPayload = platformMessage.Payload;
        MessageUrl = platformMessage.MessageUrl;
        MessageId = platformMessage.MessageId;
        ThreadId = platformMessage.ThreadId;
        IsInThread = platformMessage.IsInThread;
        ReplyInThreadMessageTarget = platformMessage.ReplyInThreadMessageTarget;

        InteractionInfo = messageEventPayload.InteractionInfo;
        IsInteraction = messageEventPayload.InteractionInfo is not null;
        Blocks = messageEventPayload.Blocks;
        Files = messageEventPayload.Files;

        SkillName = skillName;
        // Filter out bot users for now until we know what to do with them.
        var mentionUsers = platformMessage
            .Mentions
            .Where(m => !m.User.IsBot)
            .Select(m => m.ToPlatformUser());

        Arguments = new Arguments(arguments, mentionUsers);
        CommandText = commandText;
        OriginalMessage = originalMessage;
        Sigil = sigil;
        Room = platformMessage.Room ?? throw new InvalidOperationException("Room cannot be null for MessageContext");
        Mentions = platformMessage.Mentions;
        Log.CreatedMessageContext(skillName, arguments, Organization.PlatformId, Organization.PlatformType);
        Patterns = patterns;
        Scope = scope;
    }

    /// <summary>
    /// Contains information about the interaction if this message represents a user interaction with a UI element.
    /// </summary>
    public MessageInteractionInfo? InteractionInfo { get; }

    /// <summary>
    /// Sends a text response back to the chat platform.
    /// Used to respond to chat.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="inThread">If <c>true</c>, send this message in a threaded-reply if it's not already part of a
    /// thread. Defaults to <c>false</c>.</param>
    public Task SendActivityAsync(string message, bool inThread = false)
    {
        return SendActivityAsync(MessageFactory.Text(message), inThread);
    }

    /// <summary>
    /// Updates a chat message.
    /// </summary>
    /// <param name="message">The message to update.</param>
    public async Task UpdateActivityAsync(Activity message)
    {
        await Responder.UpdateActivityAsync(message);
    }

    /// <summary>
    /// Removes a chat message from the chat platform.
    /// </summary>
    /// <param name="platformRoomId">The platform-specific Id of the room the activity is in.</param>
    /// <param name="activityId">The Id of the activity. In the case of Slack, the timestamp for the message.</param>
    public async Task DeleteActivityAsync(string platformRoomId, string activityId)
    {
        await Responder.DeleteActivityAsync(platformRoomId, activityId);
    }

    /// <summary>
    /// Removes a chat message from the chat platform.
    /// </summary>
    /// <param name="responseUrl">The message response URL used to edit or delete a message.</param>
    public async Task DeleteActivityAsync(Uri responseUrl)
    {
        await Responder.DeleteActivityAsync(responseUrl);
    }

    /// <summary>
    /// Sends an activity response back to the chat platform.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="inThread">If <c>true</c>, send this message in a threaded-reply if it's not already part of a
    /// thread. Defaults to <c>false</c>.</param>
    public async Task SendActivityAsync(Activity message, bool inThread = false)
    {
        // If we're in a thread, we stay in a thread.
        // Otherwise, if `inThread` is true, we jump into a thread.
        if ((IsInThread || inThread) && ReplyInThreadMessageTarget is not null)
        {
            message.OverrideDestination(ReplyInThreadMessageTarget);
        }

        await Responder.SendActivityAsync(message);
    }

    /// <summary>
    /// Sends an activity response back to the chat platform.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="messageTarget">The target of the message. If null, targets the current thread (if in a thread) or the current room.</param>
    public async Task SendActivityAsync(Activity message, IMessageTarget messageTarget)
    {
        await Responder.SendActivityAsync(message, messageTarget);
    }

    /// <summary>
    /// Opens the view as a modal (Slack only).
    /// </summary>
    /// <param name="triggerId">A short-lived ID that can be used to <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see>.</param>
    /// <param name="view">The view to open.</param>
    public async Task OpenModalAsync(string triggerId, ViewUpdatePayload view)
    {
        if (view.CallbackId is null && SkillName is { Length: > 0 })
        {
            view = view with
            {
                CallbackId = new BuiltInSkillCallbackInfo(SkillName)
            };
        }

        await Responder.OpenModalAsync(triggerId, view);
    }

    /// <summary>
    /// Platform specific message Id that uniquely identifies this message for the purposes of API calls
    /// to the platform.
    /// </summary>
    public string? MessageId { get; }

    /// <summary>
    /// The URL to the message.
    /// </summary>
    public Uri? MessageUrl { get; }

    /// <summary>
    /// Platform specific thread Id that uniquely identifies the thread in which this message was posted. We
    /// expect this to be <c>null</c> for a top-level message.
    /// </summary>
    public string? ThreadId { get; }

    /// <summary>
    /// If <c>true</c>, this message is a reply in a thread. If <c>false</c>, this message is a top-level message in a
    /// room.
    /// </summary>
    public bool IsInThread { get; }

    /// <summary>
    /// When sending a message, pass this as the <see cref="IMessageTarget" /> in order to reply in a thread instead
    /// of as the next message in a room.
    /// </summary>
    public IMessageTarget? ReplyInThreadMessageTarget { get; }

    /// <summary>
    /// The name of the skill this event should be handled by.
    /// </summary>
    public string SkillName { get; }

    /// <summary>
    /// An optional collection of <see cref="ILayoutBlock"/> objects describing the message.
    /// This preserves any rich text formatting that may have been present in the original message.
    /// </summary>
    public IReadOnlyList<ILayoutBlock> Blocks { get; }

    /// <summary>
    /// If this message included file attachments, this is the collection of file references.
    /// </summary>
    public IReadOnlyList<FileUpload> Files { get; }

    public SkillDataScope? Scope { get; private set; }

    /// <summary>
    /// The skill data context id, which can be the room, conversation, user id or null,
    /// depending on the scope of the skill.
    /// </summary>
    /// <remarks>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// </remarks>
    public string? ContextId => Scope switch
    {
        SkillDataScope.Room => Room.Id.ToStringInvariant(),
        SkillDataScope.Conversation => Conversation?.Id.ToStringInvariant(),
        SkillDataScope.User => From.Id.ToStringInvariant(),
        _ => null
    };

    /// <summary>
    /// The arguments parsed into a nice easy to use collection.
    /// </summary>
    public IArguments Arguments { get; }

    /// <summary>
    /// The set of <see cref="SkillPattern"/> instances that were matched (if any) for this message.
    /// </summary>
    public IReadOnlyList<SkillPattern> Patterns { get; }

    /// <summary>
    /// If true, the skill is responding to the user interacting with a UI element in chat such as clicking on
    /// a button.
    /// </summary>
    [MemberNotNullWhen(true, nameof(InteractionInfo))]
    public bool IsInteraction { get; }

    /// <summary>
    /// This is the part after the Bot mention or the shortcut character. If the message is not directed at Abbot but
    /// is a command because of pattern matching or raising an AI signal, this is the full message text.
    /// </summary>
    /// <remarks>
    /// For example, with the command "@abbot who are you" or ".who are you" this would be "who are you".
    /// </remarks>
    public string CommandText { get; }

    /// <summary>
    /// The original, exact, text of the message.
    /// </summary>
    public string OriginalMessage { get; }

    /// <summary>
    /// The "sigil" applied when invoking this skill, such as the '!' character to force exact matching of arguments.
    /// </summary>
    public string Sigil { get; }

    /// <summary>
    /// The <see cref="Entities.Room"/> entity representing the room in which the message was posted.
    /// </summary>
    public Room Room { get; }

    /// <summary>
    /// The mentioned users.
    /// </summary>
    public IReadOnlyList<Member> Mentions { get; }

    /// <summary>
    /// The <see cref="Conversation"/> in which this message was posted.
    /// </summary>
    public Conversation? Conversation => ConversationMatch?.Conversation;

    /// <summary>
    /// The <see cref="ConversationMatch"/> for this message.
    /// </summary>
    public ConversationMatch? ConversationMatch { get; set; }

    public override string ToString()
    {
        return $"{Arguments.Value} From: {From}";
    }

    /// <summary>
    /// Returns a new <see cref="MessageContext"/> with the resolved skill. Update the arguments with the resolved
    /// skill, if the skill has prepended arguments such as with the
    /// <see cref="AliasSkill"/>.
    /// </summary>
    /// <remarks>
    /// This is also used when calling a user-defined skill. All user-defined skills are actually called by
    /// <see cref="RemoteSkillCallSkill" />. So we have to prepend the name of the user-defined skill when
    /// calling <see cref="RemoteSkillCallSkill" />. When passed to the user-defined skills, the skill name
    /// is removed from the beginning of the arguments.
    /// </remarks>
    /// <param name="skill">The resolved skill.</param>
    public MessageContext WithResolvedSkill(IResolvedSkill skill)
    {
        if (skill is { Arguments.Length: > 0 })
        {
            var arguments = Arguments as Arguments
                            ?? throw new InvalidOperationException("Arguments must be Arguments");

            arguments.Prepend(skill.Arguments);
        }

        Scope = skill.Scope;
        return this;
    }
}
