using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Manifests;

/// <summary>
/// A group of settings corresponding to the Settings section of the app config pages.
/// </summary>
public record ManifestSettings
{
    /// <summary>
    /// An array of strings that contain IP addresses that conform to the <a href="https://api.slack.com/authentication/best-practices#ip_allowlisting">Allowed IP Ranges feature</a>.
    /// </summary>
    [JsonProperty("allowed_ip_address_ranges")]
    [JsonPropertyName("allowed_ip_address_ranges")]
    public List<string>? AllowedIpAddressRanges { get; set; }

    /// <inheritdoc cref="ManifestEventSubscriptions" />
    [JsonProperty("event_subscriptions")]
    [JsonPropertyName("event_subscriptions")]
    public ManifestEventSubscriptions? EventSubscriptions { get; set; }

    /// <inheritdoc cref="ManifestInteractivity" />
    [JsonProperty("interactivity")]
    [JsonPropertyName("interactivity")]
    public ManifestInteractivity? Interactivity { get; set; }

    /// <summary>
    /// A boolean that specifies whether or not <a href="https://api.slack.com/enterprise/apps">org-wide deploy</a> is enabled.
    /// </summary>
    [JsonProperty("org_deploy_enabled")]
    [JsonPropertyName("org_deploy_enabled")]
    public bool? OrgDeployEnabled { get; set; }

    /// <summary>
    /// A boolean that specifies whether or not <a href="https://api.slack.com/apis/connections/socket">Socket Mode</a> is enabled.
    /// </summary>
    [JsonProperty("socket_mode_enabled")]
    [JsonPropertyName("socket_mode_enabled")]
    public bool? SocketModeEnabled { get; set; }

    /// <summary>
    /// Undocumented!
    /// </summary>
    [JsonProperty("token_rotation_enabled")]
    [JsonPropertyName("token_rotation_enabled")]
    public bool? TokenRotationEnabled { get; set; }
}

/// <summary>
/// A subgroup of settings that describe <a href="https://api.slack.com/events-api">Events API</a> configuration for the app.
/// </summary>
public record ManifestEventSubscriptions
{
    /// <summary>
    /// A string containing the full https URL that acts as the
    /// <a href="https://api.slack.com/events-api#the-events-api__subscribing-to-event-types__events-api-request-urls">Events API</a> request URL.
    /// If set, you'll need to manually verify the Request URL in the App Manifest section of App Management.
    /// </summary>
    [JsonProperty("request_url")]
    [JsonPropertyName("request_url")]
    public string? RequestUrl { get; set; }

    /// <summary>
    /// An array of strings matching the <a href="https://api.slack.com/events">event types</a> you want to the app to subscribe to.
    /// A maximum of 100 event types can be used.
    /// </summary>
    [JsonProperty("bot_events")]
    [JsonPropertyName("bot_events")]
    public List<string>? BotEvents { get; set; }

    /// <summary>
    /// An array of strings matching the <a href="https://api.slack.com/events">event types</a> you want to the app to subscribe to on behalf of authorized users.
    /// A maximum of 100 event types can be used.
    /// </summary>
    [JsonProperty("user_events")]
    [JsonPropertyName("user_events")]
    public List<string>? UserEvents { get; set; }
}

/// <summary>
/// A subgroup of settings that describe <a href="https://api.slack.com/interactivity">interactivity</a> configuration for the app.
/// </summary>
public record ManifestInteractivity
{
    /// <summary>
    /// A boolean that specifies whether or not interactivity features are enabled.
    /// </summary>
    [JsonProperty("is_enabled")]
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// A string containing the full https URL that acts as the <a href="https://api.slack.com/interactivity/handling#setup">interactive Request URL</a>.
    /// </summary>
    [JsonProperty("request_url")]
    [JsonPropertyName("request_url")]
    public string? RequestUrl { get; set; }

    /// <summary>
    /// A string containing the full https URL that acts as the <a href="https://api.slack.com/interactivity/handling#setup">interactive Options Load URL</a>.
    /// </summary>
    [JsonProperty("message_menu_options_url")]
    [JsonPropertyName("message_menu_options_url")]
    public string? MessageMenuOptionsUrl { get; set; }
}
