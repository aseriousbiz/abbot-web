using System;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Payloads;

/// <summary>
/// Slash Commands allow users to invoke your app by typing a string into the message composer box.
/// This represents an incoming slash command request. See
/// <see href="https://api.slack.com/interactivity/slash-commands">Slash Commands</see> for more info
/// </summary>
public class CommandPayload
{
    /// <summary>
    /// The command that was typed in to trigger this request. This value can be useful if you want to use a
    /// single Request URL to service multiple Slash Commands, as it lets you tell them apart.
    /// </summary>
    [JsonProperty("command")]
    [JsonPropertyName("command")]
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// This is the part of the Slash Command after the command itself, and it can contain absolutely anything that
    /// the user might decide to type. It is common to use this text parameter to provide extra context for the command.
    /// </summary>
    [JsonProperty("text")]
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// A temporary webhook URL that you can use to generate
    /// <see href="https://api.slack.com/interactivity/handling#message_responses">messages responses</see>.
    /// </summary>
    [JsonProperty("response_url")]
    [JsonPropertyName("response_url")]
    public Uri ResponseUrl { get; init; } = null!;

    /// <summary>
    /// A short-lived ID that will let your app open a modal.
    /// </summary>
    [JsonProperty("trigger_id")]
    [JsonPropertyName("trigger_id")]
    public string TriggerId { get; init; } = string.Empty;

    /// <summary>
    /// A short-lived ID that will let your app open a modal.
    /// </summary>
    [JsonProperty("user_id")]
    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// The team that the user invoked the slash command from.
    /// </summary>
    [JsonProperty("team_id")]
    [JsonPropertyName("team_id")]
    public string TeamId { get; init; } = string.Empty;

    /// <summary>
    /// The channel that the user invoked the slash command from.
    /// </summary>
    [JsonProperty("channel_id")]
    [JsonPropertyName("channel_id")]
    public string? ChannelId { get; init; }

    /// <summary>
    /// Available if the workspace is part of an Enterprise Grid.
    /// </summary>
    [JsonProperty("enterprise_id")]
    [JsonPropertyName("enterprise_id")]
    public string? EnterpriseId { get; init; }

    /// <summary>
    /// Your Slack app's unique identifier. Use this in conjunction with request signing to verify context for
    /// inbound requests.
    /// </summary>
    [JsonProperty("api_app_id")]
    [JsonPropertyName("api_app_id")]
    public string ApiAppId { get; init; } = string.Empty;
}
