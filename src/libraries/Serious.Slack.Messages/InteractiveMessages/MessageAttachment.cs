using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;

namespace Serious.Slack.InteractiveMessages;

/// <summary>
/// An attachment to attach to a Slack message. See <see href="https://api.slack.com/reference/messaging/attachments"/>
/// for more details.
/// </summary>
/// <remarks>
/// Attachments are a legacy feature of Slack. If you are using attachments, Slack still recommends that you use the
/// <see cref="Blocks"/> property to structure and layout the content within them using Block Kit.
/// </remarks>
public class MessageAttachment
{
    /// <summary>
    /// The attachment Id. This is set by the Slack API and should not be set by the user.
    /// </summary>
    [JsonProperty("id")]
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    /// <summary>
    /// An array of <see href="https://api.slack.com/block-kit/building">layout blocks</see> in the same format as
    /// described in the <see href="https://api.slack.com/block-kit/building">building blocks guide</see>.
    /// </summary>
    [JsonProperty("blocks")]
    [JsonPropertyName("blocks")]
    public IList<ILayoutBlock> Blocks { get; } = new List<ILayoutBlock>();
}
