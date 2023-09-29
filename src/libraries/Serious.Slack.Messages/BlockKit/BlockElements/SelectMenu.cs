using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;

namespace Serious.Slack.Abstractions;

/// <summary>
/// Base class for select menus. Just as with a standard HTML &lt;select&gt; tag,
/// creates a drop down menu with a list of options for a user to choose. The select
/// menu also includes type-ahead functionality, where a user can type a part or all
/// of an option string to filter the list.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/block-elements#select"/> for more
/// information.
/// </remarks>
public abstract record SelectMenu(string Type) : InteractiveElement(Type), IInputElement
{
    /// <summary>
    /// A <c>plain_text-only</c> <see cref="PlainText"/> that defines the placeholder
    /// text shown on the menu. Maximum length for this field is 150 characters.
    /// </summary>
    [JsonProperty("placeholder")]
    [JsonPropertyName("placeholder")]
    public PlainText? Placeholder { get; init; }

    /// <summary>
    /// A <see cref="ConfirmationDialog"/> that defines an optional confirmation
    /// dialog that appears before the multi-select choices are submitted.
    /// </summary>
    [JsonProperty("confirm")]
    [JsonPropertyName("confirm")]
    public ConfirmationDialog? Confirm { get; init; }
}
