using System.Collections.Generic;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Clients;

/// <summary>
/// Used to run background tasks that call the Slack API and updates entities
/// with the information retrieved.
/// </summary>
public interface IBackgroundSlackClient
{
    /// <summary>
    /// Enqueue the update of an organization's set of OAuth scopes.
    /// </summary>
    /// <param name="organization">The organization to update.</param>
    void EnqueueUpdateOrganizationScopes(Organization organization);

    /// <summary>
    /// Updates the organization info and users on a background task based on information retrieved from the Slack
    /// API.
    /// </summary>
    /// <param name="organization">The organization to update.</param>
    void EnqueueUpdateOrganization(Organization organization);

    /// <summary>
    /// Sends a message in Slack to the user that installed Abbot to a new organization as a background task.
    /// </summary>
    /// <param name="organization">The organization the user belongs to.</param>
    /// <param name="installer">The <see cref="Member"/> who installed Abbot.</param>
    void EnqueueMessageToInstaller(Organization organization, Member installer);

    /// <summary>
    /// Sends a direct message to every member.
    /// </summary>
    /// <param name="organization">The organization to send the message from.</param>
    /// <param name="members">The members to send the message to.</param>
    /// <param name="message">The message.</param>
    void EnqueueDirectMessages(Organization organization, IEnumerable<Member> members, string message);

    /// <summary>
    /// Sends a welcome direct message in Slack to a user when they are added as an Administrator to ab.bot.
    /// </summary>
    /// <param name="organization">The organization all the members belong to.</param>
    /// <param name="admin">The newly added administrator.</param>
    /// <param name="actor">The <see cref="Member"/> that added this user to the Administrators role.</param>
    void EnqueueAdminWelcomeMessage(Organization organization, Member admin, Member actor);
}
