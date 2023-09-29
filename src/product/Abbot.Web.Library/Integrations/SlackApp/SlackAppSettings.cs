using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Cryptography;

namespace Serious.Abbot.Integrations.SlackApp;

public record SlackAppSettings : IIntegrationSettings
{
#pragma warning disable CA1033
    static IntegrationType IIntegrationSettings.IntegrationType => IntegrationType.SlackApp;
#pragma warning restore CA1033

    public static string? SlackAppUrl(Integration? slackAppIntegration) => slackAppIntegration?.ExternalId is { Length: > 0 } slackAppId
        ? $"https://api.slack.com/apps/{slackAppId}"
        : null;

    public SlackManifestSettings? Manifest { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [MemberNotNullWhen(true, nameof(Manifest))]
    public bool HasManifest => Manifest is { IsValid: true };

    public SlackCredentials? Credentials { get; set; }

    [MemberNotNullWhen(true, nameof(Credentials))]
    public bool HasCredentials(Integration? slackAppIntegration) =>
        slackAppIntegration is { ExternalId.Length: > 0 }
        && this is
        {
            Credentials.HasCredentials: true,
        };

    /// <summary>
    /// Authorization for this custom Abbot, to apply to the Organization on enable.
    /// </summary>
    public SlackAuthorization? Authorization { get; set; }

    [MemberNotNullWhen(true, nameof(Authorization))]
    public bool HasAuthorization(Integration? slackAppIntegration) =>
        this is
        {
            Authorization.BotUserId.Length: > 0,
            Authorization.ApiToken.Empty: false,
        }
        && slackAppIntegration is { ExternalId: { Length: > 0 } slackAppId }
        && slackAppId == Authorization.AppId;

    /// <summary>
    /// Authorization for default Abbot, to restore to the Organization on disable.
    /// </summary>
    public SlackAuthorization? DefaultAuthorization { get; set; }
}

public record SlackAuthorization(
    string? AppId = null,
    string? AppName = null,
    string? BotId = null,
    string? BotUserId = null,
    string? BotName = null,
    string? BotAvatar = null,
    string? BotResponseAvatar = null,
    SecretString? ApiToken = null,
    string? Scopes = null)
{
    // For deserialization
    public SlackAuthorization() : this(AppId: null) { }

    public SlackAuthorization(Organization organization)
        : this(
            organization.BotAppId,
            organization.BotAppName,
            organization.PlatformBotId,
            organization.PlatformBotUserId,
            organization.BotName,
            organization.BotAvatar,
            organization.BotResponseAvatar,
            organization.ApiToken,
            organization.Scopes)
    {
    }

    public Organization Apply(Organization organization)
    {
        organization.BotAppId = AppId;
        organization.BotAppName = AppName;
        organization.PlatformBotId = BotId;
        organization.PlatformBotUserId = BotUserId;
        organization.BotName = BotName;
        organization.BotAvatar = BotAvatar;
        organization.BotResponseAvatar = BotResponseAvatar;
        organization.ApiToken = ApiToken;
        organization.Scopes = Scopes;
        return organization;
    }
}

public record SlackManifestSettings
{
    /// <summary>
    /// The name of the app.
    /// </summary>
    [Display(Name = "App Display Name")]
    [Required]
    [StringLength(35)]
    [JsonProperty("name")]
    [JsonPropertyName("name")]
    public string? AppName { get; set; }

    /// <summary>
    /// The display name of the bot user.
    /// </summary>
    [Display(Name = "Bot User Display Name")]
    [Required]
    [StringLength(80)]
    [RegularExpression("^[0-9a-zA-Z .'-]+$", ErrorMessage = "Only letters, numbers, dashes, spaces, apostrophes and periods are allowed.")]
    public string? BotUserDisplayName { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [MemberNotNullWhen(true, nameof(AppName), nameof(BotUserDisplayName))]
    public bool IsValid =>
        this is
        {
            AppName.Length: > 0,
            BotUserDisplayName.Length: > 0,
        };
}

public record SlackCredentials
{
    /// <summary>
    /// The client Id for the Custom Slack App.
    /// </summary>
    [Display(Name = "Client ID")]
    [Required]
    [JsonProperty("client_id")]
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    /// <summary>
    /// The client secret for the Custom Slack App.
    /// </summary>
    [Display(Name = "Client Secret")]
    [Required]
    [JsonProperty("client_secret")]
    [JsonPropertyName("client_secret")]
    public SecretString? ClientSecret { get; set; }

    /// <summary>
    /// The key Slack uses to sign request payloads.
    /// </summary>
    [Display(Name = "Signing Secret")]
    [Required]
    [JsonProperty("signing_secret")]
    [JsonPropertyName("signing_secret")]
    public SecretString? SigningSecret { get; set; }

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    [MemberNotNullWhen(true, nameof(ClientId), nameof(ClientSecret), nameof(SigningSecret))]
    public bool HasCredentials =>
        this is
        {
            ClientId: not null,
            ClientSecret: not null,
            SigningSecret: not null,
        };
}
