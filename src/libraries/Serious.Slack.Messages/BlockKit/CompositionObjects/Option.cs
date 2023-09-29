using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;

namespace Serious.Slack.BlockKit;

/// <summary>
/// An object that represents a single selectable item in a select menu (<see cref="SelectMenu"/>) or
/// multi-select menu (<see cref="MultiSelectMenu"/> or derived classes.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#option"/> for
/// more information.
/// </remarks>
/// <param name="Text">The <c>plain_text</c> shown in the option on the menu.</param>
/// <param name="Value">A unique string value that will be passed to your app when this option is chosen (max 75 chars).
/// </param>
public record Option<TText>(

    [property:JsonProperty("text")]
    [property:JsonPropertyName("text")]
    TText Text,

    [property:JsonProperty("value")]
    [property:JsonPropertyName("value")]
    string Value) where TText : TextObject;

/// <summary>
/// An object that represents a single selectable item in a select menu (<see cref="SelectMenu"/>) or
/// multi-select menu (<see cref="MultiSelectMenu"/> or derived classes.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#option"/> for
/// more information.
/// </remarks>
/// <param name="Text">The <c>plain_text</c> shown in the option on the menu.</param>
/// <param name="Value">A unique string value that will be passed to your app when this option is chosen (max 75 chars).
/// </param>
public record Option(PlainText Text, string Value) : Option<PlainText>(Text, Value)
{
    /// <summary>
    /// Constructs an <see cref="Option{T}"/> with empty text and value.
    /// </summary>
    /// <remarks>
    /// This is needed for deserialization.
    /// </remarks>
    public Option() : this(new PlainText(""), string.Empty)
    {
    }
}

/// <summary>
/// An object that represents a single selectable item in a checkbox group (<see cref="CheckboxGroup"/> or
/// <see cref="RadioButtonGroup"/>.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#option"/> for
/// more information.
/// </remarks>
public record CheckOption(TextObject Text, string Value) : Option<TextObject>(Text, Value)
{
    /// <summary>
    /// Constructs an <see cref="CheckOption"/> with empty text and value.
    /// </summary>
    /// <remarks>
    /// This is needed for deserialization.
    /// </remarks>
    public CheckOption() : this(new PlainText(string.Empty), string.Empty)
    {
    }

    /// <summary>
    /// Constructs a <see cref="CheckOption"/> with the given text, value, and description.
    /// </summary>
    /// <param name="text">The text shown in the option.</param>
    /// <param name="value">The value.</param>
    public CheckOption(PlainText text, string value) : this((TextObject)text, value)
    {
    }

    /// <summary>
    /// Constructs a <see cref="CheckOption"/> with the given text, value, and description.
    /// </summary>
    /// <param name="text">The text shown in the option.</param>
    /// <param name="value">The value.</param>
    public CheckOption(MrkdwnText text, string value) : this((TextObject)text, value)
    {
    }

    /// <summary>
    /// Constructs a <see cref="CheckOption"/> with the given text, value, and description.
    /// </summary>
    /// <param name="text">The text shown in the option.</param>
    /// <param name="value">The value.</param>
    /// <param name="description">The plain text description for this option.</param>
    public CheckOption(PlainText text, string value, PlainText description) : this((TextObject)text, value, description)
    {
    }

    /// <summary>
    /// Constructs a <see cref="CheckOption"/> with the given text, value, and description.
    /// </summary>
    /// <param name="text">The text shown in the option.</param>
    /// <param name="value">The value.</param>
    /// <param name="description">The plain text description for this option.</param>
    public CheckOption(PlainText text, string value, TextObject description) : this((TextObject)text, value, description)
    {
    }

    /// <summary>
    /// Constructs a <see cref="CheckOption"/> with the given text, value, and description.
    /// </summary>
    /// <param name="text">The text shown in the option.</param>
    /// <param name="value">The value.</param>
    /// <param name="description">The plain text description for this option.</param>
    public CheckOption(TextObject text, string value, TextObject description) : this(text, value)
    {
        Description = description;
    }

    /// <summary>
    /// The descriptive text shown below the <c>text</c> field beside the radio button or .
    /// checkbox. Maximum length for the <c>text</c> object within this field is 75 characters.
    /// </summary>
    [JsonProperty("description")]
    [JsonPropertyName("description")]
    public TextObject? Description { get; init; }
}

/// <summary>
/// An object that represents a single selectable item in an overflow menu (<see cref="OverflowMenu"/>.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/block-kit/composition-objects#option"/> for
/// more information.
/// </remarks>
public record OverflowOption(PlainText Text, string Value) : Option(Text, Value)
{
    /// <summary>
    /// Constructs an <see cref="OverflowOption"/> with empty text and value.
    /// </summary>
    /// <remarks>
    /// This is needed for deserialization.
    /// </remarks>
    public OverflowOption() : this(string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// Constructs an <see cref="OverflowOption"/> with the given text and value.
    /// </summary>
    /// <param name="text">The text shown in the option.</param>
    /// <param name="value">The value.</param>
    /// <param name="url">A url to load when the option is clicked.</param>
    public OverflowOption(PlainText text, string value, Uri url) : this(text, value)
    {
        Url = url;
    }

    /// <summary>
    /// A URL to load in the user's browser when the option is clicked. The url attribute is
    /// only available in overflow menus. Maximum length for this field is 3000 characters.
    /// If you're using url, you'll still receive an interaction payload and will need to
    /// send an acknowledgement response.
    /// </summary>
    [JsonProperty("url")]
    [JsonPropertyName("url")]
    public Uri? Url { get; init; }
}
