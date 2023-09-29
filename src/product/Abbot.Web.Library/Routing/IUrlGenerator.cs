using Microsoft.AspNetCore.Http;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Routing;

/// <summary>
/// Used to generate URLs for pages on the Abbot website.
/// </summary>
public interface IUrlGenerator
{
    string PublicHostName { get; }

    /// <summary>
    /// Gets the fully qualified URL to the Account settings page.
    /// </summary>
    Uri AccountSettingsPage();

    /// <summary>
    /// Gets the fully qualified URL to the Skill edit page. If host is null, attempts to use the
    /// <see cref="HttpContext" /> to fill it in.
    /// </summary>
    /// <param name="skill">The name of the skill.</param>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to the skill page.</returns>
    Uri SkillPage(string skill);

    /// <summary>
    /// Gets the fully qualified URL for the page used to configure a skill's triggers.
    /// If host is null, attempts to use the <see cref="HttpContext" /> to fill it in.
    /// </summary>
    /// <param name="skill">The name of the skill.</param>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to a skill's trigger page.</returns>
    Uri TriggerPage(string skill);

    /// <summary>
    /// Gets the fully qualified URL for the home page.
    /// If host is null, attempts to use the <see cref="HttpContext" /> to fill it in.
    /// </summary>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to Abbot's home page.</returns>
    Uri HomePage();

    /// <summary>
    /// Gets the fully qualified URL to the Admin settings page. If host is null, attempts to use the
    /// <see cref="HttpContext" /> to fill it in.
    /// </summary>
    Uri OrganizationSettingsPage();

    /// <summary>
    /// Gets the fully qualified URL to the Admin Billing page. If host is null, attempts to use the
    /// <see cref="HttpContext" /> to fill it in.
    /// </summary>
    /// <returns></returns>
    Uri OrganizationBillingPage();

    /// <summary>
    /// Gets the fully qualified URL to the Admin page to invite users. If host is null, attempts to use the
    /// <see cref="HttpContext" /> to fill it in.
    /// </summary>
    Uri InviteUsersPage();

    /// <summary>
    /// Gets the fully qualified URL to the Admin integration settings page. If host is null, attempts to use the
    /// <see cref="HttpContext" /> to fill it in.
    /// </summary>
    Uri IntegrationSettingsPage();

    /// <summary>
    /// Gets the fully qualified URL to the Admin settings page for managing users. If host is null, attempts to use the
    /// <see cref="HttpContext" /> to fill it in.
    /// </summary>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to the skill page.</returns>
    Uri OrganizationUsersSettingsPage();

    /// <summary>
    /// Gets the fully qualified URL to the Conversation Detail page for the specified conversation.
    /// </summary>
    /// <param name="conversationId">The ID of the conversation.</param>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to the conversation detail page for the conversation specified by <paramref name="conversationId"/>.</returns>
    Uri ConversationDetailPage(int conversationId);

    /// <summary>
    /// Gets the fully qualified URL to an Announcement detail page.
    /// </summary>
    /// <param name="announcementId">The Id of the announcement.</param>
    Uri AnnouncementPage(int announcementId);

    /// <summary>
    /// Gets the fully qualified URL to the Rooms Settings page.
    /// </summary>
    /// <param name="fragment">The fragment on the page to link to.</param>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to the Rooms settings page.</returns>
    Uri RoomsSettingsPage(FragmentString fragment = default);

    /// <summary>
    /// Gets the fully qualified URL to the Room Settings page for the specified conversation.
    /// </summary>
    /// <param name="room">The Room to link to.</param>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to the Room settings page for the room specified by <paramref name="room"/>.</returns>
    Uri RoomSettingsPage(Room room);

    /// <summary>
    /// Gets the fully qualified URL to a Playbook Run.
    /// </summary>
    /// <param name="run">The <see cref="PlaybookRun"/>.</param>
    Uri PlaybookRunPage(PlaybookRun run);

    /// <summary>
    /// Gets the fully qualified URL to a Playbook Run Group.
    /// </summary>
    /// <param name="group">The <see cref="PlaybookRunGroup"/>.</param>
    Uri PlaybookRunGroupPage(PlaybookRunGroup group);

    /// <summary>
    /// Returns a URL to an organization settings page.
    /// </summary>
    /// <returns>A fully qualified <see cref="Uri"/> pointing to the organization settings page.</returns>
    Uri AdvancedSettingsPage();

    /// <summary>
    /// Returns a URL to the endpoint for Zendesk events for the specified organization.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> this endpoint is for.</param>
    Uri ZendeskWebhookEndpoint(Organization organization);

    /// <summary>
    /// Returns a URL to the Manifest-approved endpoint for Auth0 login redirects.
    /// </summary>
    Uri Auth0LoginCallback();

    /// <summary>
    /// Return a URL to the Manifest-approved endpoint for Slack install redirects.
    /// </summary>
    /// <returns></returns>
    Uri SlackInstallComplete();

    /// <summary>
    /// Returns a URL to the public endpoint to receive Slack events.
    /// </summary>
    /// <param name="integration">The Slack Integration to listen for, or <c>null</c> for the default (system) Abbot.</param>
    Uri SlackWebhookEndpoint(Integration? integration);

    /// <summary>
    /// URL used to install Abbot into a Slack workspace.
    /// </summary>
    string SlackInstallUrl();

    /// <summary>
    /// Url to the page that displays information about a pending request to link a <see cref="Conversation"/> to
    /// a ticket created by the integration.
    /// </summary>
    /// <remarks>
    /// We could, in theory, always use this URL as it'll redirect to the actual ticket.
    /// </remarks>
    /// <param name="conversation">The conversation the ticket is associated to.</param>
    /// <param name="integrationType">The integration type for the ticket.</param>
    /// <param name="actor">The <see cref="Member"/> attempting to create the ticket.</param>
    /// <returns>The <see cref="Uri"/> to the ticket pending page.</returns>
    Uri PendingTicketPage(Conversation conversation, IntegrationType integrationType, Member actor);

    /// <summary>
    /// Retrieves the Webhook URL for a Playbook Webhook Trigger.
    /// </summary>
    /// <param name="playbook">The playbook.</param>
    /// <returns>
    /// For localhost we use the /api/internal/playbooks/{playbook}/trigger/{trigger}, because it runs on the same host
    /// For production we use the {TriggerHostName}/p/{playbook}/trigger/{trigger} because it's on its own host name.
    /// </returns>
    Uri GetPlaybookWebhookTriggerUrl(Playbook playbook);
}
