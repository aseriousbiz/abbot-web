using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.BlockKit;
using Serious.Slack.Converters;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack.Payloads;


/// <summary>
/// Payload for an interaction with a view. This might have a type of <c>block_actions</c>, <c>view_submission</c>
/// or <c>view_closed</c>.
/// </summary>
public interface IViewPayload : IPayload
{
    /// <summary>
    /// The source view the user initiated the interaction from. This will include the full state of the
    /// message, or the view within a modal or Home tab.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    View View { get; }
}

/// <summary>
/// A <see cref="BlockActionsPayload"/> in response to an interaction with a view.
/// </summary>
[Element("block_actions", Discriminator = "view")]
public record ViewBlockActionsPayload : BlockActionsPayload, IViewBlockActionsPayload
{
    /// <summary>
    /// The source view the user initiated the interaction from. This will include the full state of the
    /// message, or the view within a modal or Home tab. If the source view was a message, this property
    /// will be null and <see cref="SlackMessage" /> will be populated.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    public View View { get; init; } = null!;

    /// <summary>
    /// Information about the view this payload contains.
    /// </summary>
    [JsonProperty("container")]
    [JsonPropertyName("container")]
    public ViewContainer Container { get; init; } = null!;
}

/// <summary>
/// A payload sent when a modal view is submitted.
/// </summary>
[Element("view_submission")]
public record ViewSubmissionPayload() : InteractionPayload("view_submission"), IViewSubmissionPayload
{
    /// <summary>
    /// The source view the user initiated the interaction from. This will include the full state of the
    /// message, or the view within a modal or Home tab. If the source view was a message, this property
    /// will be null and <see cref="SlackMessage" /> will be populated.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    public View View { get; init; } = null!;

    /// <summary>
    /// An array of objects that contain <c>response_url</c> values, used to send message responses. Each object will
    /// also contain <c>block_id</c> and <c>action_id</c> values to identify the source of the interaction. Also
    /// included is a <c>channel_id</c> which identifies where the <c>response_url</c> will publish to.
    /// <c>response_urls</c> is available only when the view contained block elements configured to generate a
    /// <c>response_url</c>.
    /// </summary>
    [JsonProperty("response_urls")]
    [JsonPropertyName("response_urls")]
    public IReadOnlyList<ResponseInfo> ResponseUrls { get; init; } = Array.Empty<ResponseInfo>();
}

/// <summary>
/// Optionally sent to an app's configured Request URL when a user dismisses a modal.
/// To receive these payloads, the modal view must have been created with the
/// <c>notify_on_close</c> argument set to <c>true</c>.
/// </summary>
[Element("view_closed")]
public record ViewClosedPayload() : Payload("view_closed"), IViewClosedPayload
{
    /// <summary>
    /// A boolean that represents whether or not a whole view stack was cleared.
    /// </summary>
    [JsonProperty("is_cleared")]
    [JsonPropertyName("is_cleared")]
    public bool IsCleared { get; init; }

    /// <summary>
    /// The source modal view that the user dismissed. This will include the full state of the
    /// view, including any input blocks and their current values.
    /// </summary>
    [JsonProperty("view")]
    [JsonPropertyName("view")]
    public View View { get; init; } = null!;
}

/// <summary>
/// Information about the message the event payload contains.
/// </summary>
/// <param name="ViewId">The Id of the view.</param>
public record ViewContainer(
    [property:JsonProperty("view_id")]
    [property:JsonPropertyName("view_id")]
    string ViewId);

/// <summary>
/// A <c>response_url</c> with information about where to send message responses.
/// </summary>
/// <param name="BlockId">Used to identify the container that contains the source of the interaction.</param>
/// <param name="ActionId">Used to identify the source of the interaction.</param>
/// <param name="ChannelId">Identifies the channel where the message will be published to.</param>
/// <param name="ResponseUrl">The URL to POST the message to.</param>
public record ResponseInfo(
    [property:JsonProperty("block_id")]
    [property:JsonPropertyName("block_id")]
    string BlockId,

    [property:JsonProperty("action_id")]
    [property:JsonPropertyName("action_id")]
    string ActionId,

    [property:JsonProperty("channel_id")]
    [property:JsonPropertyName("channel_id")]
    string ChannelId,

    [property:JsonProperty("response_url")]
    [property:JsonPropertyName("response_url")]
    Uri ResponseUrl);
