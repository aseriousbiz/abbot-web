using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Telemetry;
using Serious.Logging;
using Serious.Slack.AspNetCore;
using Serious.Slack.BotFramework;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.Abbot.Controllers;

// This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
// implementation at runtime. Multiple different IBot implementations running at different endpoints can be
// achieved by specifying a more specific type for the bot constructor argument.
[Route("api/slack")]
[ApiController]
[IngestionHost] // The intent is for this controller to be the only endpoint that responds to in.ab.bot in production.
[AllowAnonymous]
public class SlackBotController : ControllerBase
{
    readonly IBotFrameworkAdapter _adapter;
    readonly IBot _bot;
    readonly ILogger<SlackBotController> _log;
    readonly IClock _clock;
    readonly Histogram<int> _retryCountMetric;
    readonly Histogram<long> _retryDelayMetric;

    public SlackBotController(IBotFrameworkAdapter adapter, IBot bot, ILogger<SlackBotController> log, IClock clock)
    {
        _adapter = adapter;
        _bot = bot;
        _log = log;
        _clock = clock;

        _retryCountMetric = AbbotTelemetry.Meter.CreateHistogram<int>(
            "slack.events.retryCount",
            "retries",
            "Number of retries for a given event. Will be emitted multiple times for the same event.");
        _retryDelayMetric = AbbotTelemetry.Meter.CreateHistogram<long>(
            "slack.events.retryDelay",
            "milliseconds",
            "Retry delay in milliseconds. Will be emitted multiple times for the same event.");
    }

    public async Task<IActionResult> GetAsync()
    {
        return Content("Abbot Slack Endpoint ready for action!");
    }

    [HttpPost, VerifySlackRequest]
    public async Task<IActionResult> PostAsync([FromQuery] int? integrationId)
    {
        _log.MethodEntered(typeof(SlackBotController), nameof(PostAsync), "Received Slack Request");
        var retryNum = 0;
        string? retryReason = null;
        if (Request.Headers["X-Slack-Retry-Num"] is [{ } retryNumStr, ..])
        {
            retryNum = int.Parse(retryNumStr, CultureInfo.InvariantCulture);
            var timestamp = Request.Headers["X-Slack-Request-Timestamp"].ToString();
            var requestTime = long.TryParse(timestamp, out var epochSeconds)
                ? DateTimeOffset.FromUnixTimeSeconds(epochSeconds) : (DateTimeOffset?)null;
            retryReason = Request.Headers["X-Slack-Retry-Reason"].ToString();
            var retryDelay = (long?)(_clock.UtcNow - requestTime)?.TotalMilliseconds;
            var metricTags = new TagList()
            {
                { "reason", retryReason },
            };
            _retryCountMetric.Record(retryNum, metricTags);
            if (retryDelay is not null)
            {
                _retryDelayMetric.Record(retryDelay.Value, metricTags);
            }

            if (retryNum < 3)
            {
                _log.SlackRetry(retryNum,
                    timestamp,
                    retryDelay,
                    retryReason);
            }
            else
            {
                _log.SlackFinalRetry(retryNum,
                    timestamp,
                    retryDelay,
                    retryReason);
            }
        }

        var requestBody = VerifySlackRequestAttribute.GetSlackRequestBody(HttpContext)
                          ?? throw new InvalidOperationException("Slack Request Body is not stored in HttpContext. This means the VerifySlackRequest attribute is not applied or working correctly.");
        var requestContentType = HttpContext.Request.ContentType
                                 ?? throw new InvalidOperationException("Slack sent us a request without a Content-Type header.");

        // Delegate the processing of the HTTP POST to the adapter.
        // The adapter will invoke the bot.
        return await _adapter.ProcessAsync(
            requestBody,
            requestContentType,
            _bot,
            integrationId,
            retryNum,
            retryReason,
            HttpContext.RequestAborted);
    }
}

static partial class SlackBotControllerLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Received Slack retry number {RetryNum} for request ts={Timestamp}. Delay = {Delay}ms. Reason: {RetryReason}")]
    public static partial void SlackRetry(
        this ILogger<SlackBotController> logger,
        int retryNum,
        string timestamp,
        long? delay,
        string retryReason);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Received FINAL Slack retry number {RetryNum} for request ts={Timestamp}. Delay = {Delay}ms. Reason: {RetryReason}")]
    public static partial void SlackFinalRetry(
        this ILogger<SlackBotController> logger,
        int retryNum,
        string timestamp,
        long? delay,
        string retryReason);
}
