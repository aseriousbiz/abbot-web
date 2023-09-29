using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Repositories;
using Serious.Logging;

namespace Serious.Abbot.Controllers;

[Route("/hubspot/webhook")]
[AllowAnonymous]
[VerifyHubSpotRequest]
public class HubSpotWebhookController : Controller
{
    static readonly ILogger<HubSpotWebhookController> Log = ApplicationLoggerFactory.CreateLogger<HubSpotWebhookController>();

    readonly IBackgroundJobClient _backgroundJobClient;
    readonly ISettingsManager _settingsManager;
    readonly IUserRepository _userRepository;

    public HubSpotWebhookController(
        IBackgroundJobClient backgroundJobClient,
        ISettingsManager settingsManager,
        IUserRepository userRepository)
    {
        _backgroundJobClient = backgroundJobClient;
        _settingsManager = settingsManager;
        _userRepository = userRepository;
    }

    [HttpPost]
    public async Task<IActionResult> WebhookAsync()
    {
        var abbot = await _userRepository.EnsureAbbotUserAsync();

        var requestBody = VerifyHubSpotRequestAttribute.GetHubSpotWebhookRequestBody(HttpContext).Require();
        var payloads = JsonConvert
            .DeserializeObject<IReadOnlyList<HubSpotWebhookPayload>>(requestBody)
            .Require();

        // We only care about new messages in conversations at the moment.
        foreach (var payload in payloads.Where(p => p.SubscriptionType is "conversation.newMessage"))
        {
            var threadId = payload.ObjectId;
            var messageId = payload.MessageId.Require();
            var portalId = payload.PortalId;

            // We only want to enqueue a job if we haven't already done so for this message.
            // I'm going to take the easy way out and use a Setting for this. The name and scope combo has to be
            // unique. So we should only succeed creating a setting for a given message once.
            try
            {
                var setting = await _settingsManager.SetAsync(
                   SettingsScope.HubSpotPortal(portalId),
                   name: HubSpotToSlackImporter.GetImportKey(threadId, messageId),
                   value: requestBody,
                   abbot,
                   ttl: TimeSpan.FromDays(3));
                _backgroundJobClient.Enqueue<HubSpotToSlackImporter>(i => i.ImportMessageAsync(
                    setting.Id,
                    messageId,
                    threadId,
                    portalId));
            }
            catch (DbUpdateException e) when (e.GetDatabaseError() is UniqueConstraintError { ColumnNames: [nameof(Setting.Scope), nameof(Setting.Name)] })
            {
                // We've already enqueued a job for this message. Silently ignore.
                // We don't throw because we don't want HubSpot to retry.
                Log.DuplicateHubSpotMessageAttempt(e, messageId, threadId, portalId);
            }
        }

        return Content(requestBody);
    }
}

public static partial class HubSpotWebhookControllerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Duplicate attempt to import a HubSpot message. HubSpotMessageId={HubSpotMessageId}, HubSpotThreadId={HubSpotThreadId}, PortalId={PortalId}")]
    public static partial void DuplicateHubSpotMessageAttempt(
        this ILogger<HubSpotWebhookController> logger,
        Exception ex,
        string hubSpotMessageId,
        long hubSpotThreadId,
        long portalId);
}
