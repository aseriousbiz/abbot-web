using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.AspNetCore;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Integrations.HubSpot;

/// <summary>
/// Verifies an incoming Webhook request from HubSpot according to the rules outlined in
/// <see href="https://developers.hubspot.com/docs/api/webhooks#security"/>
/// </summary>
/// <remarks>
/// <see cref="HubSpotWebhookSignatureVerificationFilter"/> must be registered in the DI container and as a filter
/// with MVC. See <see cref="HubSpotStartupExtensions"/>.
/// </remarks>
public class HubSpotWebhookSignatureVerificationFilter : IAsyncAuthorizationFilter
{
    static readonly ILogger<HubSpotWebhookSignatureVerificationFilter> Log = ApplicationLoggerFactory.CreateLogger<HubSpotWebhookSignatureVerificationFilter>();
    readonly IClock _clock;
    readonly byte[]? _clientSecretBytes;

    public HubSpotWebhookSignatureVerificationFilter(IOptions<HubSpotOptions> options, IClock clock)
    {
        _clock = clock;
        if (options.Value.ClientSecret is { } clientSecret)
        {
            _clientSecretBytes = Encoding.UTF8.GetBytes(clientSecret);
        }
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var requiresVerification = context
            .ActionDescriptor
            .EndpointMetadata
            .Any(attr => attr is VerifyHubSpotRequestAttribute);

        if (!requiresVerification)
        {
            return;
        }

        var verificationResult = await VerifyRequestAsync(context.HttpContext);
        if (!verificationResult.IsSuccessful())
        {
            Log.VerificationFailed(verificationResult.ToString());
        }
        IActionResult? result = verificationResult switch
        {
            VerificationResult.Ok => null,

            // We want the TimestampExpired to trigger an error to HubSpot so it will retry.
            VerificationResult.TimestampExpired => new ConflictResult(),

            // But other failures should just be ignored (with logging)
            VerificationResult.MissingTimestamp => new AcceptedResult(),
            VerificationResult.SignatureMismatch => new AcceptedResult(),
            VerificationResult.Ignored => new AcceptedResult(),
            _ => throw new InvalidOperationException("Could not verify the HubSpot request.")
        };

        if (result is not null)
        {
            context.Result = result;
        }
    }

    async Task<VerificationResult> VerifyRequestAsync(HttpContext httpContext)
    {
        var request = httpContext.Request;

        // We need to access the request body in order to verify the request.
        request.EnableBuffering();

        var ts = request.Headers["X-HubSpot-Request-Timestamp"].ToString();
        var hubSpotSignature = request.Headers["X-HubSpot-Signature-v3"].ToString();

        if (ts is not { Length: > 0 } || hubSpotSignature is not { Length: > 0 })
        {
            return VerificationResult.MissingTimestamp;
        }

        if (!long.TryParse(ts, out var epochMilliseconds))
        {
            throw new InvalidOperationException($"Timestamp header not an int {ts}");
        }
        DateTimeOffset messageTime = DateTimeOffset.FromUnixTimeMilliseconds(epochMilliseconds);

        if (messageTime < _clock.UtcNow.AddMinutes(-5)) // Message time is more than 5 minutes in the past
        {
            return VerificationResult.TimestampExpired;
        }

        // We don't want to dispose this stream, as it may be used by other filters, etc.
#pragma warning disable CA2000
        var reader = new StreamReader(request.Body);
#pragma warning restore CA2000
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);

        // Go back to the beginning so the controller method(s) can access the request.
        request.Body.Seek(0, SeekOrigin.Begin);

        var sigBase = $"{request.Method}{request.GetOriginalUrl()}{body}{ts}";
        using var hmac = new HMACSHA256(_clientSecretBytes.Require());

        var result = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(sigBase)));

        if (result == hubSpotSignature)
        {
            httpContext.Items[VerifyHubSpotRequestAttribute.RequestBodyKey] = body;
            return VerificationResult.Ok;
        }

        Log.SignatureMismatch(sigBase, result, hubSpotSignature);
        return VerificationResult.SignatureMismatch;
    }
}

public static partial class HubSpotWebhookSignatureVerificationFilterLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Signature mismatch, payload '{SignaturePayload}' should have signature '{ComputedSignature}', but HubSpot provided signature '{ProvidedSignature}'.")]
    public static partial void SignatureMismatch(this ILogger<HubSpotWebhookSignatureVerificationFilter> logger,
        string signaturePayload,
        string computedSignature,
        string providedSignature);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Verification failed: {VerificationResult}")]
    public static partial void VerificationFailed(this ILogger<HubSpotWebhookSignatureVerificationFilter> logger,
        string verificationResult);
}
