using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Serious.Abbot.Entities;
using Serious.Abbot.Routing;

namespace Serious.TestHelpers;

public class FakeUrlGenerator : IUrlGenerator
{
    public string PublicHostName => "test.ab.bot";

    public Uri AccountSettingsPage() => HomePage().Append("/settings/account");

    public Uri SkillPage(string skill)
    {
        return HomePage().Append($"/skills/{skill}");
    }

    public Uri TriggerPage(string skill)
    {
        return HomePage().Append($"/skills/{skill}/triggers");
    }

    public Uri HomePage()
    {
        return new Uri("https://app.ab.bot/");
    }

    public Uri OrganizationSettingsPage()
    {
        return HomePage().Append("/settings/organization");
    }

    public Uri OrganizationBillingPage()
    {
        return new Uri(OrganizationSettingsPage() + "/billing");
    }

    public Uri InviteUsersPage()
    {
        return new Uri(OrganizationUsersSettingsPage() + "/invite");
    }

    public Uri IntegrationSettingsPage()
    {
        return HomePage().Append("/settings/organization/integrations");
    }

    public Uri OrganizationUsersSettingsPage()
    {
        return new Uri(OrganizationSettingsPage() + "/users");
    }

    public Uri PlansPage()
    {
        return HomePage().Append("/plans");
    }

    public Uri ConversationDetailPage(int conversationId)
    {
        return HomePage().Append($"/conversations/{conversationId}");
    }

    public Uri AnnouncementPage(int announcementId)
    {
        return HomePage().Append($"/announcements/{announcementId}");
    }

    public Uri RoomsSettingsPage(FragmentString fragment = default)
    {
        return HomePage().Append("/settings/rooms");
    }

    public Uri RoomSettingsPage(Room room)
    {
        return HomePage().Append($"/settings/rooms/{room.PlatformRoomId}");
    }

    public Uri PlaybookRunGroupPage(PlaybookRunGroup group) =>
        HomePage().Append($"/playbooks/{group.Playbook.Slug}/groups/{group.CorrelationId}");

    public Uri AdvancedSettingsPage()
    {
        return HomePage().Append("/settings/organization/advanced");
    }

    public Uri ZendeskWebhookEndpoint(Organization organization)
    {
        return HomePage().Append($"/api/integrations/zendesk/webhook/{organization.Id}");
    }

    public Uri Auth0LoginCallback() =>
        new("https://aserioustest.us.auth0.com/login/callback");

    public Uri SlackInstallComplete() =>
        HomePage().Append($"/slack/installed");

    public Uri SlackWebhookEndpoint(Integration? integration) =>
        HomePage().Append("/api/slack" + (integration is null ? "" : $"?integrationId={integration.Id}"));

    public string SlackInstallUrl() => "/slack/install";

    public Uri PendingTicketPage(Conversation conversation, IntegrationType integrationType, Member actor)
    {
        return HomePage().Append($"/conversations/pending/{conversation.Id}/{integrationType}/{actor.Id}");
    }

    public Uri PlaybookRunPage(PlaybookRun run) =>
        HomePage().Append($"/playbooks/{run.Playbook.Slug}/runs/{run.CorrelationId}");

    public Uri GetPlaybookWebhookTriggerUrl(Playbook playbook)
    {
        return new Uri($"https://test.ab.bot/playbook/{playbook.Slug}/trigger/{playbook.GetWebhookTriggerToken()}");
    }
}
