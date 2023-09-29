using System.Diagnostics.CodeAnalysis;
using Humanizer;
using Serious.Abbot.Entities;
using Serious.Cryptography;

namespace Serious.Abbot.Integrations.GitHub;

public class GitHubSettings : IIntegrationSettings, ITicketingSettings
{
    const IntegrationType Type = IntegrationType.GitHub;
    public static IntegrationType IntegrationType => Type;
#pragma warning disable CA1033
    string ITicketingSettings.IntegrationName => Type.Humanize();
    string ITicketingSettings.IntegrationSlug => Type.Humanize(LetterCasing.LowerCase);
    ConversationLinkType ITicketingSettings.ConversationLinkType => ConversationLinkType.GitHubIssue;
#pragma warning restore CA1033

    public IntegrationLink? GetTicketLink(ConversationLink? conversationLink) => conversationLink is null
        ? null
        : GitHubIssueLink.Parse(conversationLink.ExternalId);

    public int? InstallationId { get; set; }
    public SecretString? InstallationToken { get; set; }
    public DateTime? InstallationTokenExpiryUtc { get; set; }

    /// <summary>
    /// Gets a boolean indicating if this instance has sufficient credentials to call the GitHub API.
    /// </summary>
    [MemberNotNullWhen(true, nameof(InstallationId))]
    public bool HasApiCredentials => this is { InstallationId: > 0 };

    public string? DefaultRepository { get; set; }
}
