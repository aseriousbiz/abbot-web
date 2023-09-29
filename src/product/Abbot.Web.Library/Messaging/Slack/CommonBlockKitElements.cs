using Serious.Abbot.Events;
using Serious.Abbot.PayloadHandlers;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Messaging.Slack;

/// <summary>
/// Common elements.
/// </summary>
public static class CommonBlockKitElements
{
    /// <summary>
    /// Renders a dismiss button that routes to the <see cref="DismissHandler"/>.
    /// </summary>
    /// <param name="text">The button text.</param>
    /// <param name="style">The button style.</param>
    public static ButtonElement DismissButton(string text = "Dismiss", ButtonStyle style = ButtonStyle.Default)
    {
        return new ButtonElement(Text: text, Value: "dismiss")
        {
            Style = style,
            ActionId = InteractionCallbackInfo.For<DismissHandler>()
        };
    }
}
