using Serious.Slack.BlockKit;

namespace Serious.Slack.Abstractions;

/// <summary>
/// Base class for single select menus. Just as with a standard HTML &lt;select&gt; tag,
/// creates a drop down menu with a list of options for a user to choose. The select
/// menu also includes type-ahead functionality, where a user can type a part or all
/// of an option string to filter the list.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#select"/> for more
/// information.
/// </remarks>
public abstract record SingleSelectMenu(string Type) : SelectMenu(Type), IActionElement
{
    /// <summary>
    /// The selected text value.
    /// </summary>
    public abstract string? SelectedValue { get; init; }
}
