using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.SlackApp;
using Serious.Abbot.Repositories;
using Serious.Slack.AspNetCore;

namespace Serious.Abbot.Messaging.Slack;

public class CustomSlackOptionsProvider : ISlackOptionsProvider
{
    readonly IIntegrationRepository _integrationRepository;
    readonly SlackOptions _options;

    public CustomSlackOptionsProvider(
        IIntegrationRepository integrationRepository,
        IOptions<SlackOptions> options)
    {
        _integrationRepository = integrationRepository;
        _options = options.Value;
    }

    public async Task<SlackOptions> GetOptionsAsync(HttpContext httpContext)
    {
        if (!_options.SlackSignatureValidationEnabled)
        {
            return _options;
        }

        if (!httpContext.Request.Query.TryGetValue("integrationId", out var integrationId))
            return _options;

        if (int.TryParse(integrationId, out var id))
        {
            var integration = await _integrationRepository.GetIntegrationByIdAsync(id, httpContext.RequestAborted);
            if (integration is { Type: IntegrationType.SlackApp })
            {
                var settings = _integrationRepository.ReadSettings<SlackAppSettings>(integration);
                return new SlackOptions
                {
                    AppId = integration.ExternalId,
                    SigningSecret = settings.Credentials?.SigningSecret?.Reveal(),
                    IntegrationId = id,
                    Integration = integration,
                };
            }
        }

        return new SlackOptions();
    }
}
