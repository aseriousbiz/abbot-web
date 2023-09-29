using Serious.Abbot.Scripting;

// This type is serializable and deserializable. The property setters cannot be private.
// ReSharper disable MemberCanBePrivate.Global

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents a button in a serializable format.
/// </summary>
public class ButtonMessage
{
    /// <summary>
    /// Create a button message,
    /// </summary>
    /// <param name="button">The button to create a JSON message for.</param>
    public static ButtonMessage FromButton(Button button)
    {
        return new()
        {
            Title = button.Title,
            Arguments = button.Arguments,
            Style = button.Style.ToString().ToLowerInvariant()
        };
    }

    /// <summary>
    /// The title of the button.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// The arguments passed back to the skill.
    /// </summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    /// The style for the button. This only works with Slack at the moment.
    /// </summary>
    public string Style { get; init; } = null!;
}
