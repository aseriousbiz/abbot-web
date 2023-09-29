using System;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Scripting;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Functions.Clients;

/// <summary>
/// Provides methods to send Block Kit replies back to Slack. Methods of this class only work when the skill is
/// responding to Slack.
/// </summary>
public class SlackClient : ISlack
{
    readonly IBotReplyClient _botReplyClient;
    readonly ISkillContextAccessor _skillContextAccessor;

    /// <summary>
    /// Constructs a <see cref="SlackClient"/> with the <see cref="IBotReplyClient"/> to use to actually send the
    /// replies.
    /// </summary>
    /// <param name="botReplyClient">The <see cref="IBotReplyClient"/> that makes the actual reply request.</param>
    /// <param name="skillContextAccessor">Used to access the context for the current skill.</param>
    public SlackClient(IBotReplyClient botReplyClient, ISkillContextAccessor skillContextAccessor)
    {
        _botReplyClient = botReplyClient;
        _skillContextAccessor = skillContextAccessor;
    }

    SkillContext SkillContext => _skillContextAccessor.SkillContext
         ?? throw new InvalidOperationException(
             $"{nameof(SkillContextAccessor)}.{nameof(SkillContextAccessor.SkillContext)} must be set before accessing it.");

    /// <summary>
    /// Reply to the current message with block kit blocks.
    /// </summary>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocks">The block or set of <see cref="ILayoutBlock"/> blocks that make up the mention.</param>
    /// <returns>A Task.</returns>
    public async Task ReplyAsync(string fallbackText, params ILayoutBlock[] blocks)
    {
        await ReplyWithLayoutBlocks(null, fallbackText, SlackSerializer.Serialize(blocks));
    }

    /// <summary>
    /// Reply to the current message with block kit blocks.
    /// </summary>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocks">The block or set of <see cref="ILayoutBlock"/> blocks that make up the mention.</param>
    /// <returns>A Task.</returns>
    public async Task ReplyAsync(MessageOptions options, string fallbackText, params ILayoutBlock[] blocks)
    {
        await ReplyWithLayoutBlocks(options, fallbackText, SlackSerializer.Serialize(blocks));
    }

    /// <summary>
    /// Reply to the current message with an anonymous object representing the blocks. Use this method with care as the <paramref name="blocksJson"/> string has to exactly match the JSON that the Slack API expects.
    /// </summary>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocksJson">A JSON string that the Slack API expects for blocks. This can be an individual block or an array of blocks.</param>
    /// <returns>A Task.</returns>
    public async Task ReplyAsync(string fallbackText, string blocksJson)
    {
        await ReplyWithLayoutBlocks(null, fallbackText, blocksJson);
    }

    /// <summary>
    /// Reply to the current message with an anonymous object representing the blocks. Use this method with care as the <paramref name="blocksJson"/> string has to exactly match the JSON that the Slack API expects.
    /// </summary>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocksJson">A JSON string that the Slack API expects for blocks. This can be an individual block or an array of blocks.</param>
    /// <returns>A Task.</returns>
    public async Task ReplyAsync(MessageOptions options, string fallbackText, string blocksJson)
    {
        await ReplyWithLayoutBlocks(options, fallbackText, blocksJson);
    }

    async Task ReplyWithLayoutBlocks(MessageOptions? options, string fallbackText, string blocksJson)
    {
        await _botReplyClient.SendSlackReplyAsync(fallbackText, blocksJson, options);
    }
}
