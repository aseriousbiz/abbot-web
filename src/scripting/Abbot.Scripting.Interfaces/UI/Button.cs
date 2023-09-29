using Serious.Slack.BlockKit;

namespace Serious.Abbot.Scripting;

/// <summary>
/// A button presented to a user.
/// </summary>
public class Button
{
    /// <summary>
    /// Creates an instance of a button.
    /// </summary>
    /// <param name="title">The text of the button.</param>
    public Button(string title) : this(title, title)
    {
    }

    /// <summary>
    /// Creates an instance of a button.
    /// </summary>
    /// <param name="title">The text of the button.</param>
    /// <param name="arguments">The value passed back when the button is clicked.</param>
    public Button(string title, string arguments) : this(title, arguments, ButtonStyle.Default)
    {
    }

    /// <summary>
    /// Creates an instance of a button.
    /// </summary>
    /// <param name="title">The text of the button.</param>
    /// <param name="arguments">The value passed back when the button is selected.</param>
    /// <param name="style">The style for the button (Slack only).</param>
    public Button(string title, string arguments, ButtonStyle style)
    {
        Title = title;
        Arguments = arguments;
        Style = style;
    }

    /// <summary>
    /// The text displayed on the button.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// The value passed back to the skill when this button is clicked.
    /// </summary>
    public string Arguments { get; }

    /// <summary>
    /// The style for the button. This only works with Slack at the moment.
    /// </summary>
    public ButtonStyle Style { get; }
}
