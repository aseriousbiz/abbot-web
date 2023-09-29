using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;

namespace Serious.Slack.Payloads;

/// <summary>
/// A payload received in response to user interactions with UI elements in Slack.
/// </summary>
public interface IBlockActionsPayload : IInteractionPayload
{
    /// <summary>
    /// A short-lived webhook that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#message_responses">send messages</see> in
    /// response to interactions.
    /// </summary>
    [JsonProperty("response_url")]
    [JsonPropertyName("response_url")]
    Uri ResponseUrl { get; }

    /// <summary>
    /// Contains data from the specific <see href="https://api.slack.com/reference/block-kit/interactive-components">interactive component</see>
    /// (<see cref="InteractiveElement"/>) that was used. <see href="https://api.slack.com/surfaces">App surfaces</see> can
    /// contain blocks with multiple interactive components, and each of those components can have multiple values
    /// selected by users.
    /// </summary>
    [JsonProperty("actions")]
    [JsonPropertyName("actions")]
    IReadOnlyList<IPayloadElement> Actions { get; }
}

/// <summary>
/// A <c>block_actions</c> payload is received when a user interacts with a
/// <see href="https://api.slack.com/reference/block-kit/interactive-components">Block Kit interactive component</see>.
/// </summary>
/// <remarks>
/// See <see href="https://api.slack.com/reference/interaction-payloads/block-actions"/> for more information.
/// <para>
/// Not to be confused with <see cref="InteractiveMessagePayload"/> which is received when a user interacts with a
/// legacy interactive attachment.
/// </para>
/// </remarks>
/// <seealso cref="MessageBlockActionsPayload"/>
/// <seealso cref="ViewBlockActionsPayload"/>
public abstract record BlockActionsPayload() : InteractionPayload("block_actions"), IBlockActionsPayload
{
    /// <summary>
    /// A short-lived webhook that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#message_responses">send messages</see> in
    /// response to interactions.
    /// </summary>
    [JsonProperty("response_url")]
    [JsonPropertyName("response_url")]
    public Uri ResponseUrl { get; init; } = null!;

    /// <summary>
    /// Contains data from the specific <see href="https://api.slack.com/reference/block-kit/interactive-components">interactive component</see>
    /// (<see cref="InteractiveElement"/>) that was used. <see href="https://api.slack.com/surfaces">App surfaces</see> can
    /// contain blocks with multiple interactive components, and each of those components can have multiple values
    /// selected by users.
    /// </summary>
    [JsonProperty("actions")]
    [JsonPropertyName("actions")]
    public IReadOnlyList<IPayloadElement> Actions { get; init; } = Array.Empty<IPayloadElement>();
}
