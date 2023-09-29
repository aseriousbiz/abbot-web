using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack;
using Serious.Slack.Converters;
using Serious.Slack.Payloads;

namespace Serious.Payloads;

/// <summary>
/// A payload sent from Slack when an external select menu needs to be populated by
/// an external data source. <see href="https://api.slack.com/reference/block-kit/block-elements#external_multi_select"/>
/// for details.
/// </summary>
[Element("block_suggestion")]
public record BlockSuggestionPayload() : InteractionPayload("block_suggestion"), IViewPayload
{
    /// <summary>
    /// Information about the container of the select menu.
    /// </summary>
    [JsonProperty("container")]
    [JsonPropertyName("container")]
    public ViewContainer Container { get; init; } = null!;

    /// <summary>
    /// The Action Id of the the select menu that is requesting the data.
    /// </summary>
    [JsonProperty("action_id")]
    [JsonPropertyName("action_id")]
    public string ActionId { get; init; } = null!;

    /// <summary>
    /// The Block Id of the block that contains the select menu that is requesting the data.
    /// </summary>
    [JsonProperty("block_id")]
    [JsonPropertyName("block_id")]
    public string BlockId { get; init; } = null!;

    /// <summary>
    /// The current value of the select menu.
    /// </summary>
    [JsonProperty("value")]
    [JsonPropertyName("value")]
    public string Value { get; init; } = null!;

    /// <summary>
    /// The view the select menu is in.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    public View View { get; init; } = null!;
}
