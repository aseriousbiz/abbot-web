using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack.Manifests;

/// <summary>
/// Manifests are configurations bundles for Slack apps.
/// With a manifest, you can use a UI or an API to create an app with a pre-defined configuration,
/// or adjust the configuration of existing apps.
/// </summary>
public record Manifest
{
    /// <inheritdoc cref="ManifestDisplayInformation" />
    [JsonProperty("display_information")]
    [JsonPropertyName("display_information")]
    public required ManifestDisplayInformation DisplayInformation { get; set; }

    /// <inheritdoc cref="ManifestFeatures" />
    [JsonProperty("features")]
    [JsonPropertyName("features")]
    public ManifestFeatures? Features { get; set; }

    /// <inheritdoc cref="ManifestOAuthConfig" />
    [JsonProperty("oauth_config")]
    [JsonPropertyName("oauth_config")]
    public ManifestOAuthConfig? OAuthConfig { get; set; }

    /// <inheritdoc cref="ManifestSettings" />
    [JsonProperty("settings")]
    [JsonPropertyName("settings")]
    public ManifestSettings? Settings { get; set; }
}

/// <summary>
/// A group of settings that describe parts of an app's appearance within Slack.
/// </summary>
/// <param name="Name">
/// The name of the app.
/// Maximum length is 35 characters.
/// </param>
/// <param name="Description">
/// A short description of the app for display to users.
/// Maximum length is 140 characters.</param>
/// <param name="BackgroundColor">
/// A hex color value (including the hex sign) that specifies the background color used on hovercards that display information about your app.
/// Can be 3-digit (<c>#000</c>) or 6-digit (<c>#000000</c>) hex values.
/// Once an app has set a background color value, it cannot be removed, only updated.
/// </param>
/// <param name="LongDescription">
/// A longer version of the description of the app.
/// Maximum length is 4000 characters.
/// </param>
public record ManifestDisplayInformation(
    [property:JsonProperty("name")]
    [property:JsonPropertyName("name")]
    string Name,

    [property:JsonProperty("description")]
    [property:JsonPropertyName("description")]
    string? Description = null,

    [property:JsonProperty("background_color")]
    [property:JsonPropertyName("background_color")]
    string? BackgroundColor = null,

    [property:JsonProperty("long_description")]
    [property:JsonPropertyName("long_description")]
    string? LongDescription = null);
