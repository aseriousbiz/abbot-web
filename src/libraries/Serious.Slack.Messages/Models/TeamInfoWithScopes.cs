using System.Diagnostics.CodeAnalysis;

namespace Serious.Slack;

/// <summary>
/// Information about a Slack Team. This is returned by the <c>team.info</c> endpoint.
/// The scopes are populated from the request headers.
/// </summary>
/// <remarks>
/// https://api.slack.com/methods/team.info
/// </remarks>
public record TeamInfoWithScopes : TeamInfo
{
    /// <summary>
    /// Construct a <see cref="TeamInfoWithScopes"/> from a <see cref="TeamInfo"/> and the scopes.
    /// </summary>
    /// <param name="teamInfo">The <see cref="TeamInfo"/> returned by the Slack API.</param>
    /// <param name="scopes">The set of OAuth scopes in the X-OAuth-Scopes response header.</param>
    [SetsRequiredMembers]
    public TeamInfoWithScopes(TeamInfo teamInfo, string scopes) : base(teamInfo)
    {
        Scopes = scopes;
        Id = teamInfo.Id;
    }

    /// <summary>
    /// These are the set of OAuth scopes that the team has granted to the application.
    /// </summary>
    public string Scopes { get; }
}
