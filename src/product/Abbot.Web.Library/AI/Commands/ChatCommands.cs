using System.Globalization;

namespace Serious.Abbot.AI.Commands;

[Command(
    "chat.post",
    """Posts a message in Slack's mrkdwn format to the current channel. If the answer is "synthesized", the `synthesized` flag should be true""",
    Exemplar = """
    {
        "body": "Hello! This is *Bold*, this is _italic_, <https://www.google.com|this is a link>",
        "synthesized": false
    }
    """)]
public record ChatPostCommand() : Command("chat.post")
{
    /// <summary>
    /// Gets or inits the body of the message to post.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Gets or inits a value indicating whether this command was synthesized by the AI.
    /// </summary>
    public bool Synthesized { get; init; }

    public override string ToString()
        => $"chat.post(\"{Body.Replace("\"", "\\\"", StringComparison.Ordinal)}\", synthesized: {Synthesized.ToString().ToLower(CultureInfo.InvariantCulture)})";
}
