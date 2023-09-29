using Serious.Abbot.Entities;

namespace Serious.Slack.AspNetCore;

/// <summary>
/// Slack configuration configured in the "Slack" section of App Settings.
/// </summary>
public class SlackOptions
{
    public const string Slack = nameof(Slack);

    /// <summary>
    /// If these options are for a custom slack app, gets or sets the requested <c>IntegrationId</c>.
    /// If this has a value but <see cref="Organization"/> is <c>null</c> the Integration was not found.
    /// </summary>
    public int? IntegrationId { get; set; }

    /// <summary>
    /// If these options are for a custom slack app, gets or sets the <see cref="Integration"/>.
    /// </summary>
    public Integration? Integration { get; set; }

    /// <summary>
    /// The Slack App ID.
    /// </summary>
    public string? AppId { get; set; }

    /// <summary>
    /// The key Slack uses to sign request payloads.
    /// </summary>
    public string? SigningSecret { get; set; }

    /// <summary>
    /// The client Id for the Abbot Slack App.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The client secret for the Abbot Slack App.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// The set of required Slack OAuth scopes needed to run Abbot. These are embedded in the install URL.
    /// </summary>
    public string? RequiredScopes { get; set; }

    /// <summary>
    /// The set of Slack OAuth scopes needed to run Abbot when using a custom app. These are embedded in the install URL.
    /// </summary>
    /// <remarks>
    /// The non-custom production app is in the Slack Marketplace.
    /// This means Slack has to approve new scopes before we can use them.
    /// However, Custom Apps are not in the marketplace, so we can use any scopes we want.
    /// This setting will generally contain the "next" set of scopes we're waiting on approval for,
    /// but there's no need to hold up Custom App users waiting for those scopes to be approved.
    /// </remarks>
    public string? CustomAppScopes { get; set; }

    /// <summary>
    /// The URL to configure the installation of Abbot in Slack.
    /// </summary>
    public string? AppConfigurationUrl { get; set; }

    /// <summary>
    /// If <c>false</c>, Slack request signatures will not be validated. Defaults to <c>true</c>.
    /// </summary>
    public bool SlackSignatureValidationEnabled { get; set; } = true;
}
