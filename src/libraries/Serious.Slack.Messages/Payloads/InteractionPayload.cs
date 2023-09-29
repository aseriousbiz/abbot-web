using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Payloads;

/// <summary>
/// Base class for payloads that are in response to user actions with UI elements (and not system UI such
/// as closing a modal dialog).
/// </summary>
public interface IInteractionPayload : IPayload
{
    /// <summary>
    /// A short-lived ID that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see>.
    /// </summary>
    [JsonProperty("trigger_id")]
    [JsonPropertyName("trigger_id")]
    string TriggerId { get; }
}

/// <summary>
/// Base class for payloads that are in response to user actions with UI elements (and not system UI such
/// as closing a modal dialog).
/// </summary>
/// <param name="Type">The interaction type.</param>
public abstract record InteractionPayload(string Type) : Payload(Type), IInteractionPayload
{
    /// <summary>
    /// A short-lived ID that can be used to
    /// <see href="https://api.slack.com/interactivity/handling#modal_responses">open modals</see>.
    /// </summary>
    [JsonProperty("trigger_id")]
    [JsonPropertyName("trigger_id")]
    public string TriggerId { get; init; } = null!;
}
