using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework.Model;

namespace Serious.Abbot.Messaging;

/// <summary>
/// Extensions to the <see cref="IResponder"/> class.
/// </summary>
public static class ResponderExtensions
{
    /// <summary>
    /// Sends an activity response back to the chat platform using the supplied <paramref name="message"/>.
    /// </summary>
    /// <param name="responder">The <see cref="IResponder"/> this method extends.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="messageTarget">The target to send the message to.</param>
    public static Task SendActivityAsync(this IResponder responder, string message, IMessageTarget? messageTarget = null)
    {
        return responder.SendActivityAsync(MessageFactory.Text(message), messageTarget);
    }

    /// <summary>
    /// Sends a rich formatted ephemeral message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="responder">The <see cref="IResponder"/> this method extends.</param>
    /// <param name="userId">The platform-specific user ID of the recipient.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="messageTarget">The target to post to.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task SendEphemeralActivityAsync(
        this IResponder responder,
        string userId,
        string fallbackText,
        IMessageTarget? messageTarget,
        params ILayoutBlock[] blocks)
    {
        var richActivity = new RichActivity(fallbackText, blocks)
        {
            EphemeralUser = userId,
        };
        await responder.SendActivityAsync(richActivity, messageTarget);
    }

    /// <summary>
    /// Sends a rich formatted ephemeral message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="responder">The <see cref="IResponder"/> this method extends.</param>
    /// <param name="userId">The platform-specific user ID of the recipient.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task SendEphemeralActivityAsync(
        this IResponder responder,
        string userId,
        string fallbackText,
        params ILayoutBlock[] blocks) => await responder.SendEphemeralActivityAsync(
            userId,
            fallbackText,
            null,
            blocks);

    /// <summary>
    /// Sends a rich formatted ephemeral message with the specified Block Kit layout blocks.
    /// </summary>
    /// <param name="responder">The <see cref="IResponder"/> this method extends.</param>
    /// <param name="member">The <see cref="Member"/> to send this to.</param>
    /// <param name="fallbackText">The text to use as a fallback in case the blocks are malformed or not supported.</param>
    /// <param name="messageTarget">The target to post to.</param>
    /// <param name="blocks">The Slack Block Kit blocks that comprise the formatted message.</param>
    public static async Task SendEphemeralActivityAsync(
        this IResponder responder,
        Member member,
        string fallbackText,
        IMessageTarget? messageTarget,
        params ILayoutBlock[] blocks) => await responder.SendEphemeralActivityAsync(
            member.User.PlatformUserId,
            fallbackText,
            messageTarget,
            blocks);

}
