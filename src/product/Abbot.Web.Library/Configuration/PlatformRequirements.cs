using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Slack.AspNetCore;

namespace Serious.Abbot.Configuration;

/// <summary>
/// Used to determine which scopes are required for an organization and whether a specified scope
/// is required.
/// </summary>
/// <remarks>
/// This is currently Slack only. We use this to help determine which messages to respond to based on the
/// installed scopes. This is because we used to only listen to the `app_mention` event. But now we support both
/// `app_mention` and `message` events. However, an mention of abbot raises both these events, and we only want
/// to respond to one. Thus we need to know which scopes they have installed in order to make that decision.
/// </remarks>
public class PlatformRequirements
{
    readonly HashSet<string> _requiredScopes;
    readonly HashSet<string> _customAppScopes;
    readonly string? _defaultAppId;

    /// <summary>
    /// Constructs a <see cref="PlatformRequirements" /> for Slack.
    /// </summary>
    /// <param name="slackOptions"><see cref="SlackOptions"/> containing the required scopes.</param>
    public PlatformRequirements(IOptions<SlackOptions> slackOptions)
    {
        _defaultAppId = slackOptions.Value.AppId;
        _requiredScopes = ParseScopes(slackOptions.Value.RequiredScopes.Require());
        _customAppScopes = slackOptions.Value.CustomAppScopes is { Length: > 0 }
            ? ParseScopes(slackOptions.Value.CustomAppScopes)
            : _requiredScopes;
    }

    static HashSet<string> ParseScopes(string input) =>
        input.Split(',').ToHashSet();

    /// <summary>
    /// Returns true if the organization has all the scopes we require. If not, we'll need to ask them
    /// to reinstall Abbot.
    /// </summary>
    /// <param name="organization">The organization.</param>
    public bool HasRequiredScopes(Organization organization)
    {
        return organization is not { PlatformType: PlatformType.Slack }
               || organization.Scopes is null
               || !MissingScopes(organization.Scopes, organization.BotAppId).Any();
    }

    public IEnumerable<string> MissingScopes(Organization organization)
    {
        var existingScopes = organization.Scopes ?? string.Empty;
        return MissingScopes(existingScopes, organization.BotAppId);
    }

    public IEnumerable<string> MissingScopes(SlackAuthorization auth) =>
        MissingScopes(auth.Scopes ?? "", auth.AppId);

    IEnumerable<string> MissingScopes(string existingScopes, string? appId) =>
        // Except returns all items in the first list that are not in the second list.
        // AKA, all the required scopes that are not in the existing scopes.
        (appId is { Length: > 0 } && appId != _defaultAppId ? _customAppScopes : _requiredScopes)
            .Except(existingScopes.Split(','));
}
