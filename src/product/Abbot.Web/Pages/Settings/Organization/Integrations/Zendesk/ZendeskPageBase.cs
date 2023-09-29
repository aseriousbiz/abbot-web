using Microsoft.Extensions.Options;
using Serious.Abbot.Integrations.Zendesk;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.Zendesk;

public abstract class ZendeskPageBase : SingleIntegrationPageBase<ZendeskSettings>
{
    public IOptions<ZendeskOptions> ZendeskOptions { get; }

    protected ZendeskPageBase(IIntegrationRepository integrationRepository, IOptions<ZendeskOptions> zendeskOptions)
        : base(integrationRepository)
    {
        ZendeskOptions = zendeskOptions;
    }

    public bool HasSubdomain => Settings.Subdomain is { Length: > 0 };

    public bool IsInstalled => Settings is
    {
        HasApiCredentials: true,
        TriggerCategoryId.Length: > 0,
        CommentPostedTriggerId.Length: > 0,
        WebhookToken.Length: > 0,
        WebhookId.Length: > 0,
    };
}
