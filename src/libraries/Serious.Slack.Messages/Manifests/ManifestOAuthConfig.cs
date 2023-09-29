using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Manifests;

/// <summary>
/// A group of settings describing OAuth configuration for the app.
/// </summary>
public record ManifestOAuthConfig
{
    /// <summary>
    /// An array of strings containing <a href="https://api.slack.com/authentication/oauth-v2#redirect_urls">OAuth redirect URLs</a>.
    /// A maximum of 1000 redirect URLs can be included in this array.
    /// </summary>
    [JsonProperty("redirect_urls")]
    [JsonPropertyName("redirect_urls")]
    public List<string>? RedirectUrls { get; set; }

    /// <inheritdoc cref="ManifestOAuthScopes" />
    [JsonProperty("scopes")]
    [JsonPropertyName("scopes")]
    public ManifestOAuthScopes? Scopes { get; set; }
}

/// <summary>
/// A subgroup of settings that describe <a href="https://api.slack.com/scopes">permission scopes</a> configuration.
/// </summary>
public record ManifestOAuthScopes
{
    /// <summary>
    /// An array of strings containing <a href="https://api.slack.com/scopes">user scopes</a> to request upon app installation.
    /// A maximum of 255 scopes can included in this array.
    /// </summary>
    [JsonProperty("user")]
    [JsonPropertyName("user")]
    public List<string>? User { get; set; }

    /// <summary>
    /// An array of strings containing <a href="https://api.slack.com/scopes">bot scopes</a> to request upon app installation.
    /// A maximum of 255 scopes can included in this array.
    /// </summary>
    [JsonProperty("bot")]
    [JsonPropertyName("bot")]
    public List<string>? Bot { get; set; }
}
