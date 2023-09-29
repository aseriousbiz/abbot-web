using Serious.Slack.Abstractions;

namespace Serious.Slack.BlockKit;

/// <summary>
/// The valid <c>type</c> (<see cref="TextObject.Type"/>) values for text objects (aka <see cref="TextObject"/>).
/// </summary>
public static class TextObjectTypes
{
    /// <summary>
    /// This is Slack's version of Markdown.
    /// </summary>
    public const string Markdown = "mrkdwn";

    /// <summary>
    /// Plain text.
    /// </summary>
    public const string PlainText = "plain_text";
}
