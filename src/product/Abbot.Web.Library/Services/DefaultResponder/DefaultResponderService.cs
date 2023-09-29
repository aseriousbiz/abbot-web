using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Segment;
using Serious.Abbot.AI;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Telemetry;
using Serious.Logging;

namespace Serious.Abbot.Services.DefaultResponder;

public class DefaultResponderService : IDefaultResponderService
{
    static readonly ILogger<DefaultResponderService> Log = ApplicationLoggerFactory.CreateLogger<DefaultResponderService>();

    readonly HttpClient _httpClient;
    readonly IOpenAIClient _openAiClient;
    readonly AISettingsRegistry _aiSettingsRegistry;
    readonly IOptions<AbbotOptions> _options;
    readonly FeatureService _featureService;
    readonly IAnalyticsClient _analyticsClient;

    readonly Histogram<long> _fallbackDurationMetric;

    public DefaultResponderService(
        HttpClient httpClient,
        IOpenAIClient openAiClient,
        AISettingsRegistry aiSettingsRegistry,
        IOptions<AbbotOptions> options,
        FeatureService featureService,
        IAnalyticsClient analyticsClient)
    {
        _httpClient = httpClient;
        _openAiClient = openAiClient;
        _aiSettingsRegistry = aiSettingsRegistry;
        _options = options;
        _featureService = featureService;
        _analyticsClient = analyticsClient;
        _fallbackDurationMetric = AbbotTelemetry.Meter.CreateHistogram<long>("responder.fallback.duration");
    }

    public async Task<string> GetResponseAsync(string message, string? address, Member member, Organization organization)
    {
        var useAI = await _featureService.IsEnabledAsync(
            FeatureFlags.AIEnhancements,
            organization);

        useAI &= organization.Settings?.AIEnhancementsEnabled ?? false;

        string? response = null;
        bool success = false;
        try
        {
            response = useAI
                ? await GetChatGptResponseAsync(message, member)
                : await GetFallbackResponseAsync(message, address);
            success = true;
        }
        catch (Exception e)
        {
            Log.EventProcessingCancelled(e, address);
        }

        _analyticsClient.Track(
            "Default responder response",
            AnalyticsFeature.Services,
            member,
            organization,
            new {
                success,
                responder = useAI ? "chatgpt" : "default"
            });

        return response ?? $"I tried to think of a great answer, but couldn't. Try again later… if this happens again please contact support at {WebConstants.SupportEmail}.";
    }

    async Task<string?> GetChatGptResponseAsync(string message, Member member)
    {
        var responderSettings = await _aiSettingsRegistry.GetModelSettingsAsync(AIFeature.DefaultResponder);
        var stopwatch = Stopwatch.StartNew();
        var result = await _openAiClient.SafelyGetCompletionAsync(
            message, responderSettings.Model, responderSettings.Temperature, member);
        if (result?.Completions is not [var response, ..])
        {
            return null;
        }

        return member.IsStaff()
            ? $"{response}\n\n_Elapsed: {stopwatch.ElapsedMilliseconds}ms, Server Elapsed: {result.ProcessingTime.TotalMilliseconds}ms_"
            : $"{response}";
    }

    async Task<string?> GetFallbackResponseAsync(string message, string? address)
    {
        var urlBase = _options.Value.DefaultResponderEndpoint;
        var query = $"&q={message}";
        if (address is not null)
        {
            query += $"&loc={address}";
        }

        var responderEndpoint = new Uri(urlBase + query);
        using var _ = _fallbackDurationMetric.Time();
        var response = await _httpClient.GetAsync(responderEndpoint);

        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadAsStringAsync()
            : null;
    }
}

public static partial class DefaultResponderServiceLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Exception attempting to get a default response. {Address}.")]
    public static partial void EventProcessingCancelled(
        this ILogger<DefaultResponderService> logger,
        Exception exception,
        string? address);
}
