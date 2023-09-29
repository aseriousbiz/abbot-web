using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Routing;

/// <summary>
/// Used to generate URLs for pages on the Abbot website.
/// </summary>
public class UrlGenerator : IUrlGenerator
{
    readonly LinkGenerator _linkGenerator;
    readonly AbbotOptions _options;
    readonly Auth0Options _auth0Options;

    /// <summary>
    /// Constructs a <see cref="UrlGenerator"/>.
    /// </summary>
    public UrlGenerator(
        LinkGenerator linkGenerator,
        IOptions<AbbotOptions> options,
        IOptions<Auth0Options> auth0Options)
    {
        _linkGenerator = linkGenerator;
        _options = options.Value;
        _auth0Options = auth0Options.Value;
    }

    public string PublicHostName =>
        _options.PublicHostName is { Length: > 0 } publicHost
            ? publicHost
            : throw new InvalidOperationException("Missing required setting 'Abbot:PublicHostName'.");

    string PublicIngestionHostName =>
        _options.PublicIngestionHostName is { Length: > 0 } publicIngestionHost
            ? publicIngestionHost
            : PublicHostName;

    public Uri SkillPage(string skill)
    {
        return GetPage("/Skills/Edit",
            new {
                skill
            });
    }

    public Uri TriggerPage(string skill)
    {
        return GetPage("/Skills/Triggers/Index",
            new {
                skill
            });
    }

    public Uri HomePage()
    {
        return GetPage("/Index");
    }

    public Uri AccountSettingsPage()
    {
        return GetPage("/Settings/Account/Index");
    }

    public Uri OrganizationSettingsPage()
    {
        return GetPage("/Settings/Organization/Index");
    }

    public Uri OrganizationBillingPage()
    {
        return GetPage("/Settings/Organization/Billing");
    }

    public Uri InviteUsersPage()
    {
        return GetPage("/Settings/Organization/Users/Invite/Index");
    }

    public Uri IntegrationSettingsPage()
    {
        return GetPage("/Settings/Organization/Integrations/Index");
    }

    public Uri OrganizationUsersSettingsPage()
    {
        return GetPage("/Settings/Organization/Users/Index");
    }

    public Uri ConversationDetailPage(int conversationId)
    {
        return GetPage("/Conversations/View",
            new {
                conversationId
            });
    }

    public Uri AnnouncementPage(int announcementId)
    {
        return GetPage("/Announcements/View", new { id = announcementId });
    }

    public Uri RoomsSettingsPage(FragmentString fragment = default)
    {
        return GetPage("/Settings/Rooms/Index", fragment: fragment);
    }

    public Uri RoomSettingsPage(Room room)
    {
        return GetPage("/Settings/Rooms/Room", new { roomId = room.PlatformRoomId });
    }

    public Uri PlaybookRunGroupPage(PlaybookRunGroup group)
    {
        return GetPage("/Playbooks/Runs/Group", new { group.Playbook.Slug, groupId = group.CorrelationId });
    }

    public Uri AdvancedSettingsPage()
    {
        return GetPage("/Settings/Organization/Advanced/Index");
    }

    public Uri ZendeskWebhookEndpoint(Organization organization)
    {
        return GetAction("Webhook",
            "Zendesk",
            new {
                organizationId = organization.Id
            },
            forcePublicHost: true);
    }

    public string SlackInstallUrl() => _linkGenerator.GetPathByAction("Install", "Slack").Require();

    public Uri PendingTicketPage(Conversation conversation, IntegrationType integrationType, Member actor)
    {
        return GetPage("/Conversations/Pending",
            new {
                conversationId = conversation.Id,
                integrationType,
                actorId = actor.Id,
            });
    }

    public Uri PlaybookRunPage(PlaybookRun run)
    {
        return GetPage("/Playbooks/Runs/View", new { run.Playbook.Slug, runId = run.CorrelationId });
    }

    static readonly string? StandaloneTriggerHost =
        AllowedHosts.Trigger.Except(AllowedHosts.Web).FirstOrDefault();

    public Uri GetPlaybookWebhookTriggerUrl(Playbook playbook) =>
        GetUrlByName(
            StandaloneTriggerHost is null ? "playbook-customer-trigger-local" : "playbook-customer-trigger",
            new {
                slug = playbook.Slug,
                apiToken = playbook.GetWebhookTriggerToken()
            },
            StandaloneTriggerHost);

    public Uri Auth0LoginCallback() =>
        new($"https://{_auth0Options.Domain.Require()}/login/callback");

    public Uri SlackInstallComplete() =>
        GetAction("InstallComplete", "Slack");

    public Uri SlackWebhookEndpoint(Integration? integration) =>
        GetAction("Post", "SlackBot", new HostString(PublicIngestionHostName), new { integrationId = integration?.Id });

    Uri GetAction(string action, string controller, object? values = null, bool forcePublicHost = false) =>
        GetAction(action, controller, new HostString(GetHost(forcePublicHost)), values);

    Uri GetAction(string action, string controller, HostString host, object? values = null)
    {
        var url = _linkGenerator.GetUriByAction(
            action,
            controller,
            values,
            WebConstants.DefaultScheme,
            host) ?? throw new InvalidOperationException($"Could not generate a URL for {controller}/{action}");

        return new Uri(url);
    }

    Uri GetUrlByName(
        string endpointName,
        object? routeValues,
        string? host = default,
        PathString pathBase = default,
        FragmentString fragment = default)
    {
        var url = _linkGenerator.GetUriByName(
            endpointName,
            routeValues,
            WebConstants.DefaultScheme,
            new HostString(host ?? GetHost(forcePublicHost: false)),
            pathBase: pathBase,
            fragment: fragment);

        if (url is null)
        {
            throw new InvalidOperationException($"Could not generate a URL for {endpointName}");
        }

        return new Uri(url);
    }

    string GetHost(bool forcePublicHost)
    {
        return forcePublicHost ? PublicHostName : WebConstants.DefaultHost;
    }

    Uri GetPage(string page, object? values = null, bool forcePublicHost = false, FragmentString fragment = default)
    {
        var url = _linkGenerator.GetUriByPage(
            page,
            handler: null,
            values,
            WebConstants.DefaultScheme,
            new HostString(GetHost(forcePublicHost)),
            fragment: fragment);

        if (url is null)
        {
            throw new InvalidOperationException($"Could not generate a URL for {page}");
        }

        return new Uri(url);
    }
}
