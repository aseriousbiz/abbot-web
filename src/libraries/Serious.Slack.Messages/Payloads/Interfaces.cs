using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;

namespace Serious.Slack.BlockKit;

/// <summary>
/// Payload received when a user interacts with a Block Kit UI element in a modal view.
/// </summary>
public interface IMessageBlockActionsPayload : IBlockActionsPayload, IMessagePayload<SlackMessage>
{
    /// <summary>
    /// Information about the message this payload contains.
    /// </summary>
    [JsonProperty("container")]
    [JsonPropertyName("container")]
    MessageContainer Container { get; }
}

/// <summary>
/// Payload received when a user interacts with a modal view.
/// </summary>
public interface IViewBlockActionsPayload : IViewPayload, IBlockActionsPayload
{
    /// <summary>
    /// Information about the view this payload contains.
    /// </summary>
    [JsonProperty("container")]
    [JsonPropertyName("container")]
    ViewContainer Container { get; }
}

/// <summary>
/// Payload received when a user closes a view.
/// </summary>
public interface IViewClosedPayload : IViewPayload
{
    /// <summary>
    /// A boolean that represents whether or not a whole view stack was cleared.
    /// </summary>
    [JsonProperty("is_cleared")]
    [JsonPropertyName("is_cleared")]
    bool IsCleared { get; }
}


/// <summary>
/// Payload received when a user submits a modal view.
/// </summary>
public interface IViewSubmissionPayload : IViewPayload, IInteractionPayload
{
    /// <summary>
    /// An array of objects that contain <c>response_url</c> values, used to send message responses. Each object will
    /// also contain <c>block_id</c> and <c>action_id</c> values to identify the source of the interaction. Also
    /// included is a <c>channel_id</c> which identifies where the <c>response_url</c> will publish to.
    /// <c>response_urls</c> is available only when the view contained block elements configured to generate a
    /// <c>response_url</c>.
    /// </summary>
    [JsonProperty("response_urls")]
    [JsonPropertyName("response_urls")]
    IReadOnlyList<ResponseInfo> ResponseUrls { get; }
}
