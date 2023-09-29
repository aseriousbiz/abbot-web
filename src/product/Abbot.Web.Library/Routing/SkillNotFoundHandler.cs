using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Messaging;
using Serious.Abbot.Services.DefaultResponder;
using Serious.Abbot.Telemetry;
using Serious.Text;

namespace Serious.Abbot.Services;

public class SkillNotFoundHandler : ISkillNotFoundHandler
{
    readonly IDefaultResponderService _defaultResponderService;
    readonly IAuditLog _auditLog;

    public SkillNotFoundHandler(IDefaultResponderService defaultResponderService,
        IAuditLog auditLog)
    {
        _defaultResponderService = defaultResponderService;
        _auditLog = auditLog;
    }

    public async Task HandleSkillNotFoundAsync(MessageContext messageContext)
    {
        var organization = messageContext.Organization;

        if (messageContext.Organization.FallbackResponderEnabled)
        {
            var defaultResponse = await _defaultResponderService.GetResponseAsync(
                messageContext.CommandText,
                messageContext.FromMember.FormattedAddress,
                messageContext.FromMember,
                organization);

            await messageContext.SendActivityAsync(defaultResponse);
            await _auditLog.LogSkillNotFoundAsync(
                messageContext.CommandText,
                defaultResponse,
                ResponseSource.AutoResponder,
                messageContext.From,
                organization);
        }
        else
        {
            await messageContext.SendActivityAsync(
                $@"Sorry, I did not understand that. `{messageContext.Bot} help` to learn what I can do.");
        }
    }
}
