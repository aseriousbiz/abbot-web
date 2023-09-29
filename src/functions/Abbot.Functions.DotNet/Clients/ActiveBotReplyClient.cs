using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Logging;

namespace Serious.Abbot.Functions.Services;

/// <summary>
/// Used to send replies back to chat via the Bot Framework.
/// </summary>
public class ActiveBotReplyClient : IBotReplyClient
{
    static readonly ILogger<ActiveBotReplyClient> Log = ApplicationLoggerFactory.CreateLogger<ActiveBotReplyClient>();

    readonly ISkillApiClient _apiClient;
    readonly IEnvironment _environment;
    readonly ISkillContextAccessor _skillContextAccessor;

    /// <summary>
    /// Constructs an <see cref="ActiveBotReplyClient"/>.
    /// </summary>
    /// <param name="apiClient">The <see cref="ISkillApiClient"/> used to call skill runner APIs.</param>
    /// <param name="environment"></param>
    /// <param name="skillContextAccessor">A <see cref="ISkillContextAccessor"/> used to access the current <see cref="Runtime.SkillContext"/>.</param>
    /// <exception cref="InvalidOperationException"></exception>
    public ActiveBotReplyClient(
        ISkillApiClient apiClient,
        IEnvironment environment,
        ISkillContextAccessor skillContextAccessor)
    {
        _apiClient = apiClient;
        _environment = environment;
        _skillContextAccessor = skillContextAccessor;
    }

    Uri ReplyUrl => _environment.GetAbbotReplyUrl(SkillContext.SkillRunnerInfo.SkillId);

    public SkillContext SkillContext => _skillContextAccessor.SkillContext
        ?? throw new InvalidOperationException($"The {nameof(SkillContext)} needs to be set for this request.");

    /// <summary>
    /// Sends a reply to the bot framework that contains UI elements in the form of buttons.
    /// </summary>
    /// <param name="reply">The reply to send.</param>
    /// <param name="delay">A delay, if any, before sending the reply.</param>
    /// <param name="options">Options to apply to the message</param>
    /// <returns>A response with information about the posted message, if successful.</returns>
    public async Task<ProactiveBotMessageResponse> SendReplyAsync(string reply, TimeSpan delay, MessageOptions? options)
    {
        return await SendProactiveMessageAsync(reply, delay, options: options);
    }

    /// <summary>
    /// Sends a rich formatted reply back to the caller when the caller is on Slack.
    /// </summary>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="options">Options to apply to the message</param>
    /// <param name="blocksJson">A JSON string that the Slack API expects for blocks. This can be an individual block or an array of blocks.</param>
    /// <returns>A response with information about the posted message, if successful.</returns>
    public async Task<ProactiveBotMessageResponse> SendSlackReplyAsync(
        string fallbackText,
        string blocksJson,
        MessageOptions? options)
    {
        return await SendProactiveMessageAsync(
            fallbackText,
            TimeSpan.Zero,
            options: options,
            blocksJson: blocksJson);
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
    public async Task<ProactiveBotMessageResponse> SendReplyAsync(
        string reply,
        TimeSpan delay,
        IEnumerable<Button> buttons,
        string? buttonsLabel,
        string? image,
        string? title,
        Uri? titleUrl,
        string? color,
        MessageOptions? options)
    {
        var buttonModels = buttons
            .Select(ButtonMessage.FromButton)
            .ToList();
        return await SendProactiveMessageAsync(
            reply,
            delay,
            buttonModels,
            buttonsLabel,
            image,
            title,
            titleUrl,
            color,
            options);
    }

    async Task<ProactiveBotMessageResponse> SendProactiveMessageAsync(
        string reply,
        TimeSpan delay,
        List<ButtonMessage>? buttons = null,
        string? buttonsLabel = null,
        string? image = null,
        string? title = null,
        Uri? titleUrl = null,
        string? color = null,
        MessageOptions? options = null,
        string? blocksJson = null)
    {
        Log.MethodEntered(GetType(), nameof(SendProactiveMessageAsync), ReplyUrl);

        var botReply = new ProactiveBotMessage
        {
            SkillId = SkillContext.SkillRunnerInfo.SkillId,
            Message = reply,
            ConversationReference = SkillContext.CreateConversationReference(),
            Schedule = (long)delay.TotalSeconds,
            Options = ProactiveBotMessageOptions.FromMessageOptions(options),
            ContextId = SkillContext.SkillRunnerInfo.ContextId,
            Blocks = blocksJson
        };

        if (buttons is { Count: > 0 } || image is { Length: > 0 })
        {
            botReply.Attachments = new List<MessageAttachment>
            {
                new()
                {
                    Buttons = buttons,
                    ButtonsLabel = buttonsLabel,
                    ImageUrl = image,
                    Title = title,
                    TitleUrl = titleUrl?.ToString(),
                    Color = color
                }
            };
        }

        var response = await _apiClient.SendJsonAsync<ProactiveBotMessage, ProactiveBotMessageResponse>(
            ReplyUrl,
            HttpMethod.Post,
            botReply) ?? ProactiveBotMessageResponse.Empty;

        if (delay == TimeSpan.Zero)
        {
            DidReply = DidReply || response.Success;
        }

        return response;
    }

    public bool DidReply { get; private set; }

    public IEnumerable<string> Replies => Enumerable.Empty<string>();
}
