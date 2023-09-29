using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Services;

/// <summary>
/// Used to send replies back to chat via the <see cref="ActiveBotReplyClient"/> or
/// <see cref="PassiveBotReplyClient"/> based on whether the incoming <see cref="SkillMessage"/> has a
/// ConversationReference or not.
/// </summary>
/// <remarks>
/// <para>
/// When calling a skill from chat, the incoming <see cref="SkillMessage" /> will have a ConversationReference
/// we can use to asynchronously reply back to chat.
/// </para>
/// <para>
/// However, when we call a skill from the Bot Console or abbot-cli, we don't have a ConversationReference. In
/// that case, the replies in a skill are collected by <see cref="PassiveBotReplyClient" /> and sent as part of
/// the response body to the request to call this skill.
/// </para>
/// </remarks>
public class BotReplyClient : IBotReplyClient
{
    readonly ActiveBotReplyClient _activeBotReplyClient;
    readonly PassiveBotReplyClient _passiveBotReplyClient = new();

    /// <summary>
    /// Constructs a <see cref="BotReplyClient"/>.
    /// </summary>
    /// <param name="activeBotReplyClient">The <see cref="ActiveBotReplyClient"/> to use when a conversation reference is available.</param>
    public BotReplyClient(ActiveBotReplyClient activeBotReplyClient)
    {
        _activeBotReplyClient = activeBotReplyClient;
    }

    IBotReplyClient ReplyClient => !_activeBotReplyClient.SkillContext.PassiveReplies
        ? _activeBotReplyClient
        : _passiveBotReplyClient;

    /// <summary>
    /// Sends a reply back to the bot framework.
    /// </summary>
    /// <param name="reply">The reply to send.</param>
    /// <param name="delay">A delay, if any, before sending the reply.</param>
    /// <param name="options">Options to apply to the message</param>
    /// <returns>A response with information about the posted message, if successful.</returns>
    public async Task<ProactiveBotMessageResponse> SendReplyAsync(string reply, TimeSpan delay, MessageOptions? options)
    {
        return await ReplyClient.SendReplyAsync(reply, delay, options);
    }

    /// <summary>
    /// Sends a rich formatted reply back to the caller when the caller is on Slack.
    /// </summary>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocksJson">A JSON string that the Slack API expects for blocks. This can be an individual block or an array of blocks.</param>
    /// <param name="options">Options to apply to the message</param>
    /// <returns>A response with information about the posted message, if successful.</returns>
    public async Task<ProactiveBotMessageResponse> SendSlackReplyAsync(
        string fallbackText,
        string blocksJson,
        MessageOptions? options)
    {
        return await ReplyClient.SendSlackReplyAsync(fallbackText, blocksJson, options);
    }

    /// <summary>
    /// Sends a reply to the bot framework that contains UI elements in the form of buttons.
    /// </summary>
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
    public async Task<ProactiveBotMessageResponse> SendReplyAsync(string reply, TimeSpan delay, IEnumerable<Button> buttons, string? buttonsLabel, string? image,
        string? title, Uri? titleUrl, string? color, MessageOptions? options)
    {
        return await ReplyClient.SendReplyAsync(reply, delay, buttons, buttonsLabel, image, title, titleUrl, color, options);
    }

    /// <summary>
    /// Returns true if at least one reply was sent with a delay of <see cref="TimeSpan.Zero"/>.
    /// </summary>
    public bool DidReply => ReplyClient.DidReply;

    /// <summary>
    /// The set of replies that need to be sent with the response. In the case of an
    /// <see cref="ActiveBotReplyClient"/> this will be empty because the responses
    /// were already sent asynchronously.
    /// </summary>
    public IEnumerable<string> Replies => ReplyClient.Replies;
}
