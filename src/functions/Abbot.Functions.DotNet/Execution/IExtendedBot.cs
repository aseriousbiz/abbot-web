using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions;

/// <summary>
/// IBot with additional methods and properties we want to use in our internal skills
/// but not expose to public skills (yet).
/// </summary>
public interface IExtendedBot : IBot
{
    /// <summary>
    /// Whether or not the skill replied.
    /// </summary>
    bool DidReply { get; }

    /// <summary>
    /// The set of replies if the skill does not reply directly to the bot.
    /// </summary>
    IReadOnlyList<string> Replies { get; }

    /// <summary>
    /// Skill data scope information. This is used to load skill state, primarily for long-running ink skills.
    /// </summary>
    SkillDataScope Scope { get; }

    /// <summary>
    /// Id that tracks the state and context of chat messages.
    /// If the skill scope is set to user, this is the UserId
    /// If the skill scope is set to Conversation, this is the ConversationId
    /// If the skill scope is set to Room, this is the RoomId
    /// </summary>
    string? ContextId { get; }

    /// <summary>
    /// The extra brain bits that aren't ready for mainstream consumption.
    /// </summary>
    IExtendedBrain ExtendedBrain { get; }

    /// <summary>
    /// Sends a reply to the chat.
    /// </summary>
    /// <param name="text">The reply message.</param>
    new Task<ProactiveBotMessageResponse> ReplyAsync(string text);

    /// <summary>
    /// Sends a message to the user that called the skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    new Task<ProactiveBotMessageResponse> ReplyAsync(string text, MessageOptions options);

    /// <summary>
    /// Sends a reply along with a set of buttons. Clicking a button will call back into this skill.
    /// </summary>
    /// <param name="text">The reply message.</param>
    /// <param name="buttons">The set of buttons to display (Maximum 6).</param>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    new Task<ProactiveBotMessageResponse> ReplyWithButtonsAsync(string text, IEnumerable<Button> buttons, MessageOptions? options = null);
}
