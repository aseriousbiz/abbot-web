using System.Threading.Tasks;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Provides methods to send Block Kit replies back to Slack. Methods of this class only work when the skill is
/// responding to Slack.
/// </summary>
public interface ISlack
{
    /// <summary>
    /// Reply to the current message with block kit blocks.
    /// </summary>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocks">The block or set of <see cref="ILayoutBlock"/> blocks that make up the mention.</param>
    /// <returns>A Task.</returns>
    Task ReplyAsync(string fallbackText, params ILayoutBlock[] blocks);

    /// <summary>
    /// Reply to the current message with block kit blocks.
    /// </summary>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocks">The block or set of <see cref="ILayoutBlock"/> blocks that make up the mention.</param>
    /// <returns>A Task.</returns>
    Task ReplyAsync(MessageOptions options, string fallbackText, params ILayoutBlock[] blocks);

    /// <summary>
    /// Reply to the current message with an anonymous object representing the blocks. Use this method with care as the <paramref name="blocksJson"/> string has to exactly match the JSON that the Slack API expects.
    /// </summary>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocksJson">A JSON string that the Slack API expects for blocks. This can be an individual block or an array of blocks.</param>
    /// <returns>A Task.</returns>
    Task ReplyAsync(string fallbackText, string blocksJson);

    /// <summary>
    /// Reply to the current message with an anonymous object representing the blocks. Use this method with care as the <paramref name="blocksJson"/> string has to exactly match the JSON that the Slack API expects.
    /// </summary>
    /// <param name="options">Options for sending the message, such as the target conversation/thread, etc.</param>
    /// <param name="fallbackText">Alternative text to show in places where block kit is not supported such as in a notification for the message.</param>
    /// <param name="blocksJson">A JSON string that the Slack API expects for blocks. This can be an individual block or an array of blocks.</param>
    /// <returns>A Task.</returns>
    Task ReplyAsync(MessageOptions options, string fallbackText, string blocksJson);
}
