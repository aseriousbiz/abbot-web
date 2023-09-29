using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Serious.Abbot;
using Serious.Abbot.Telemetry;

namespace Serious.Slack.AspNetCore;

/// <summary>
/// An authorization filter used to verify incoming Slack requests. As a convenience, if the request is valid,
/// the body of the Slack request is stored in HttpContext.Items["Slack:RequestBody"]
/// </summary>
/// <remarks>
/// <see cref="SlackRequestVerificationFilter"/> must be registered
/// in the DI container and as a filter with MVC.
/// See <see cref="StartupExtensions"/>.
/// </remarks>
public class SlackRequestVerificationFilter : IAsyncAuthorizationFilter
{
    readonly ISlackOptionsProvider _slackOptionsProvider;
    readonly ILogger<SlackRequestVerificationFilter> _logger;
    static readonly Counter<long> VerificationsCountMetric = AbbotTelemetry.Meter.CreateCounter<long>(
        "slack.verifications.count",
        "verifications",
        "The number of Slack request verifications that have occurred.");

    /// <summary>
    /// Constructs a <see cref="SlackRequestVerificationFilter"/> with the given Slack options pulled from the
    /// <paramref name="slackOptionsProvider"/> configured in App Settings.
    /// </summary>
    /// <param name="slackOptionsProvider">A provider of <see cref="SlackOptions"/>, typically from config.</param>
    /// <param name="logger">A logger.</param>
    public SlackRequestVerificationFilter(
        ISlackOptionsProvider slackOptionsProvider,
        ILogger<SlackRequestVerificationFilter> logger)
    {
        _slackOptionsProvider = slackOptionsProvider;
        _logger = logger;
    }

    /// <summary>
    /// This is just here so we can test the filter.
    /// </summary>
    public DateTimeOffset? Now { get; set; }

    /// <summary>
    /// Validate the incoming Slack Request.
    /// </summary>
    /// <param name="context">The <see cref="AuthorizationFilterContext"/>.</param>
    /// <exception cref="InvalidOperationException">Thrown if we cannot attempt to verify the request for some reason.</exception>
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var requiresVerification = context.ActionDescriptor
            .EndpointMetadata
            .Any(attr => attr is VerifySlackRequestAttribute);

        if (!requiresVerification)
        {
            return;
        }

        try
        {
            var options = await _slackOptionsProvider.GetOptionsAsync(context.HttpContext);
            using var _ = _logger.BeginOrganizationScope(options.Integration?.Organization);

            var verificationResult = await VerifyRequestAsync(context.HttpContext, options);
            VerificationsCountMetric.Add(1, new TagList
            {
                { "organization_id", options.Integration?.Organization?.PlatformId },
                { "is_custom", options.Integration is not null },
                { "slack_app_id", options.AppId },
                { "verification_result", verificationResult.ToString() },
            });

            if (!verificationResult.IsSuccessful())
            {
                _logger.VerificationFailed(verificationResult.ToString(), options.IntegrationId);
            }

            IActionResult? result = verificationResult switch
            {
                VerificationResult.Ok => null,

                // We want the TimestampExpired to trigger an error to Slack so it will retry.
                VerificationResult.TimestampExpired => new ConflictResult(),

                // But other failures should just be ignored (with logging)
                // They are bad data from Slack (or an imposter), not a problem with our app.
                VerificationResult.MissingTimestamp => new AcceptedResult(),
                VerificationResult.SignatureMismatch => new AcceptedResult(),
                VerificationResult.Ignored => new AcceptedResult(),
                _ => throw new InvalidOperationException("Could not verify the slack request.")
            };

            if (result is not null)
            {
                context.Result = result;
            }
        }
        catch (Exception ex)
        {
            _logger.VerificationException(ex);
            context.Result = new StatusCodeResult(500);
        }
    }

    async Task<VerificationResult> VerifyRequestAsync(HttpContext httpContext, SlackOptions options)
    {
        var request = httpContext.Request;

        // Slack's signed secrets mechanism requires a comparison to the
        // K/V form from the body, in a specific order (the order sent).
        // More details here: https://api.slack.com/docs/verifying-requests-from-slack
        // Rewinding is still necessary in ActionFilters
        request.EnableBuffering();

        var ts = request.Headers["X-Slack-Request-Timestamp"].ToString();
        var slackSignature = request.Headers["X-Slack-Signature"].ToString();

        if (ts is not { Length: > 0 } || slackSignature is not { Length: > 0 })
        {
            return VerificationResult.MissingTimestamp;
        }

        // We don't want to dispose this stream, as it may be used by other filters, etc.
#pragma warning disable CA2000
        var reader = new StreamReader(request.Body);
#pragma warning restore CA2000
        var body = await reader.ReadToEndAsync(httpContext.RequestAborted).ConfigureAwait(false);

        // Go back to the beginning so the controller method(s) can access the request.
        request.Body.Seek(0, SeekOrigin.Begin);

        var result = !options.SlackSignatureValidationEnabled
            ? VerificationResult.Ok
            : options is { SigningSecret.Length: > 0 }
                ? Verification.VerifyRequest(
                    body,
                    ts,
                    slackSignature,
                    options.SigningSecret,
                    Now ?? DateTimeOffset.UtcNow,
                    _logger)
                : VerificationResult.Ignored;

        if (result is VerificationResult.Ok)
        {
            httpContext.Items[VerifySlackRequestAttribute.RequestBodyKey] = body;
            return VerificationResult.Ok;
        }

        return result;
    }
}

public static partial class SlackRequestVerificationFilterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Verification failed: {VerificationResult} (IntegrationId={IntegrationId})")]
    public static partial void VerificationFailed(this ILogger<SlackRequestVerificationFilter> logger,
        string verificationResult,
        int? integrationId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Exception during verification")]
    public static partial void VerificationException(this ILogger<SlackRequestVerificationFilter> logger, Exception ex);
}
