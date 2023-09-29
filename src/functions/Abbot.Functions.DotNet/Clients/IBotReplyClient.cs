using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Services;

/// <summary>
/// Used to send replies from a skill back to caller of the skill.
/// </summary>
public interface IBotReplyClient
{
    /// <summary>
    /// Sends a reply back to the caller.
    /// </summary>
    /// <param name="reply">The reply to send.</param>
    /// <param name="delay">A delay, if any, before sending the reply.</param>
    /// <param name="options">Options to apply to the message</param>
    /// <returns>A response with information about the posted message, if successful.</returns>
    Task<ProactiveBotMessageResponse> SendReplyAsync(string reply, TimeSpan delay, MessageOptions? options);

    /// <summary>
    /// Sends a rich formatted reply back to the caller when the caller is on Slack.
    /// </summary>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocksJson">A JSON string with the block or set of blocks that make up the message.</param>
    /// <param name="options">Options to apply to the message</param>
    /// <returns>A response with information about the posted message, if successful.</returns>
    Task<ProactiveBotMessageResponse> SendSlackReplyAsync(
        string fallbackText,
        string blocksJson,
        MessageOptions? options);

    /// <summary>
    /// Sends a reply to the caller that contains UI elements in the form of buttons.
    /// </summary>
    /// <remarks>
    /// This only works for Slack.
    /// </remarks>
    /// <param name="reply">The reply to send.</param>
    /// <param name="delay">A delay, if any, before sending the reply.</param>
    /// <param name="buttons">The set of buttons to include.</param>
    /// <param name="buttonsLabel">(optional) The text that serves as a label for the set of buttons.</param>
    /// <param name="image">Either the URL to an image or the base64 encoded image.</param>
    /// <param name="title">(optional) A title to render</param>
    /// <param name="titleUrl">(optional) If specified, makes the title a link to this URL.</param>
    /// <param name="color">The color to use for the sidebar (Slack Only) in hex (ex. #3AA3E3).</param>
    /// <param name="options">Options to apply to the message</param>
    /// <returns>A response with information about the posted message, if successful.</returns>
    Task<ProactiveBotMessageResponse> SendReplyAsync(
        string reply,
        TimeSpan delay,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        string? image,
        string? title,
        Uri? titleUrl,
        string? color,
        MessageOptions? options);

    /// <summary>
    /// Returns true if at least one reply was sent with a delay of <see cref="TimeSpan.Zero"/>.
    /// </summary>
    bool DidReply { get; }

    /// <summary>
    /// The set of replies that need to be sent with the response. In the case of an
    /// <see cref="ActiveBotReplyClient"/> this will be empty because the responses
    /// were already sent asynchronously.
    /// </summary>
    public IEnumerable<string> Replies { get; }
}
