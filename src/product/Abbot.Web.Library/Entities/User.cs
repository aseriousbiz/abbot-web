using System.Collections.Generic;
using System.Diagnostics;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents an authenticated user. This maps 1-1 to an Auth0 user.
/// </summary>
[DebuggerDisplay("{DisplayName} ({PlatformUserId} Id: {Id})")]
public class User : EntityBase<User>
{
    /// <summary>
    /// The ID of the user on their chat platform.
    /// </summary>
    public string PlatformUserId { get; set; } = null!;

    /// <summary>
    /// The ID of the user retrieved from the authentication provider such as Auth0.
    /// </summary>
    /// <remarks>This must be unique across all users in the system.</remarks>
    public string? NameIdentifier { get; set; }

    /// <summary>
    /// The Slack Team Id of the user.
    /// </summary>
    /// <remarks>
    /// This is only set when the user has authenticated into https://app.ab.bot/ using their Slack account.
    /// It's populated from the https://schemas.ab.bot/platform_id claim which is set by our
    /// "Retrieve Chat Platform Info" script in Auth0 (under Auth Pipeline -> Rules).
    /// </remarks>
    public string? SlackTeamId { get; set; }

    /// <summary>
    /// Display name for the user.
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// Real name for the user, if available. Required by Slack.
    /// </summary>
    public string? RealName { get; set; }

    /// <summary>
    /// The email for the user according to the chat platform.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The avatar for the user according to the chat platform.
    /// </summary>
    public string? Avatar { get; set; }

    /// <summary>
    /// The set of <see cref="Organization"/> memberships associated with this user. In Slack,
    /// this is usually a single collection, but Enterprise Grid users can be members of multiple
    /// organizations.
    /// </summary>
    public IList<Member> Members { get; set; } = new List<Member>();

    /// <summary>
    /// Denotes whether or not the user is a bot user such as the Abbot user.
    /// </summary>
    public bool IsBot { get; set; }

    /// <summary>
    /// Denotes whether or not the user is a current/former Abbot user.
    /// </summary>
    public bool IsAbbot { get; set; }

    /// <summary>
    /// Returns a string with the Slack user mention syntax for the user.
    /// </summary>
    /// <remarks>
    /// For now, we just use Slack as that's where our priorities lie.
    /// </remarks>
    /// <returns>The Slack formatted user mention.</returns>
    public string ToMention()
    {
        return SlackFormatter.UserMentionSyntax(PlatformUserId);
    }
}
