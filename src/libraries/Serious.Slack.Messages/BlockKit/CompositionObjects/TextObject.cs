using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.Converters;

namespace Serious.Slack.Abstractions
{
    /// <summary>
    /// An object containing some text, formatted either as <c>plain_text</c> or using <c>mrkdwn</c>,
    /// our proprietary contribution to the much beloved Markdown standard.
    /// </summary>
    /// <remarks>
    /// See <see href="https://api.slack.com/reference/block-kit/composition-objects#text"/> for
    /// more information.
    /// </remarks>
    /// <param name="Type">The formatting to use for this text object. Can be one of <c>plain_text</c> or <c>mrkdwn</c>.</param>
    /// <param name="Text">
    /// The text for the block. This field accepts any of the standard
    /// <see href="https://api.slack.com/reference/surfaces/formatting">text formatting markup</see>
    /// when type is mrkdwn.
    /// </param>
    public abstract record TextObject(
        [property:JsonProperty("text")]
        [property:JsonPropertyName("text")]
        string Text,

        [property:JsonProperty("type")]
        [property:JsonPropertyName("type")]
        string Type) : IContextBlockElement;
}

namespace Serious.Slack.BlockKit
{
    /// <summary>
    /// A <c>mrkdwn</c> text object (<see cref="TextObject"/>).
    /// </summary>
    /// <param name="Text">
    /// The text for the block. This field accepts any of the standard
    /// <see href="https://api.slack.com/reference/surfaces/formatting">text formatting markup</see>.
    /// </param>
    /// <param name="Verbatim">
    /// When set to <c>false</c> (as is default) URLs will be auto-converted into links,
    /// conversation names will be link-ified, and certain mentions will be
    /// <see href="https://api.slack.com/reference/surfaces/formatting#automatic-parsing">automatically parsed.</see>
    /// Using a value of <c>true</c> will skip any preprocessing of this nature, although you can still include
    /// manual parsing strings. This field is only usable when type is <c>mrkdwn</c>.
    /// </param>
    [Element(TextObjectTypes.Markdown)]
    [Newtonsoft.Json.JsonConverter(typeof(NoConverter))]
    // ReSharper disable once IdentifierTypo
    public record MrkdwnText(
        string Text,

        [property: JsonProperty("verbatim")]
        [property: JsonPropertyName("verbatim")]
        bool Verbatim = false) : TextObject(Text,
        TextObjectTypes.Markdown);

    /// <summary>
    /// A <c>plain_text</c> text object (<see cref="TextObject"/>).
    /// </summary>
    /// <param name="Text">
    /// The text for the block.
    /// </param>
    /// <param name="Emoji">Indicates whether emojis in a text field should be escaped into the colon emoji format. Default <c>true</c>.</param>
    [Element(TextObjectTypes.PlainText)]
    [Newtonsoft.Json.JsonConverter(typeof(NoConverter))]
    public record PlainText(
        string Text,

        [property: JsonProperty("emoji")]
        [property: JsonPropertyName("emoji")]
        bool Emoji = true) : TextObject(Text, TextObjectTypes.PlainText)
    {
        /// <summary>
        /// Creates a new <see cref="PlainText"/> with the specified text. This is an alternative using the
        /// implicit conversion from string.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static PlainText FromString(string value) => new(value);

        /// <summary>
        /// Implicit conversion from <see cref="string"/> to <see cref="PlainText"/>.
        /// </summary>
        /// <param name="value">The plain text.</param>
        /// <returns>A plain text object.</returns>
        public static implicit operator PlainText(string value) => FromString(value);

        /// <summary>
        /// Returns the text of this <see cref="PlainText"/>.
        /// </summary>
        /// <returns>The text of this object.</returns>
        public override string ToString()
        {
            return Text;
        }
    };
}
