using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Azure;
using Azure.AI.TextAnalytics;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Services;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.AI;

/// <summary>
/// Wraps an Azure Cognitive Services <see cref="ITextAnalyticsClient"/>.
/// </summary>
public class AzureCognitiveServicesTextAnalyticsClient : ITextAnalyticsClient
{
    static readonly ILogger<AzureCognitiveServicesTextAnalyticsClient> Log = ApplicationLoggerFactory.CreateLogger<AzureCognitiveServicesTextAnalyticsClient>();

    readonly TextAnalyticsClient? _client;
    readonly Histogram<int> _redactionRequestsMetric;
    readonly Histogram<int> _redactedEntitiesMetric;
    readonly Histogram<int> _fallbackRedactionCountMetric;
    readonly Histogram<int> _redactionContentLengthMetric;

    public AzureCognitiveServicesTextAnalyticsClient(IOptions<CognitiveServicesOptions> options)
    {
        _redactedEntitiesMetric = AbbotTelemetry.Meter.CreateHistogram<int>("ai.redaction.redacted-entities", "entities");
        _redactionContentLengthMetric = AbbotTelemetry.Meter.CreateHistogram<int>("ai.redaction.content-length", "characters");
        _redactionRequestsMetric = AbbotTelemetry.Meter.CreateHistogram<int>("ai.redaction.requests", "requests");
        _fallbackRedactionCountMetric = AbbotTelemetry.Meter.CreateHistogram<int>("ai.redaction.fallback");

        if (options.Value.Endpoint is not null && Uri.TryCreate(options.Value.Endpoint, UriKind.Absolute, out var endpoint))
        {
            _client = new TextAnalyticsClient(endpoint, new DefaultAzureCredential());
        }
        else
        {
            Log.AzureCognitiveServicesNotConfigured();
            _client = null;
        }
    }

    public async Task<RedactResult> RecognizePiiEntitiesAsync(
        string document,
        string? language = null,
        RecognizePiiEntitiesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (document is { Length: 0 })
        {
            return RedactResult.Empty;
        }

        if (_client is null)
        {
            return FallbackRedact(document);
        }

        var metricTags = new TagList();
        try
        {
            var result = await _client.RecognizePiiEntitiesAsync(document, language, options, cancellationToken);
            _redactedEntitiesMetric.Record(result.Value.Count);

            metricTags.SetSuccess();
            _redactionRequestsMetric.Record(1, metricTags);
            _redactionContentLengthMetric.Record(document.Length, metricTags);
            return new RedactResult(result.Value);
        }
        catch (RequestFailedException e)
        {
            metricTags.SetFailure(e.ErrorCode);
            _redactionRequestsMetric.Record(1, metricTags);
            _redactionContentLengthMetric.Record(document.Length, metricTags);
            Log.FailedToCallCognitiveServices(e);

            return FallbackRedact(document);
        }
    }

    RedactResult FallbackRedact(string text)
    {
        // If we fail, might as well do the bare minimum we can.
        // This allows us to deploy this code before we have the Azure Cognitive Services configured.
        var sensitiveValues = SensitiveDataSanitizer.ScanEmails(text);
        _fallbackRedactionCountMetric.Record(1);
        return new RedactResult(text, Array.Empty<TextAnalyticsWarning>(), sensitiveValues.ToList());
    }
}

static partial class AzureCognitiveServicesTextAnalyticsClientLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Azure Cognitive Services `CognitiveServices:Endpoint` not configured")]
    public static partial void AzureCognitiveServicesNotConfigured(this ILogger<AzureCognitiveServicesTextAnalyticsClient> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Exception calling cognitive services.")]
    public static partial void FailedToCallCognitiveServices(
        this ILogger<AzureCognitiveServicesTextAnalyticsClient> logger,
        Exception exception);
}
