using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Options;
using OpenAI_API;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Embedding;
using OpenAI_API.Models;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

namespace Serious.Abbot.AI;

public class OpenAIClient : IOpenAIClient
{
    readonly OpenAIOptions _openAiOptions;
    readonly MetricSet _chatMetrics;
    readonly MetricSet _completionMetrics;
    readonly MetricSet _embeddingsMetrics;

    class MetricSet
    {
        public required Histogram<int> TokenCount { get; init; }
        public required Histogram<long> Duration { get; init; }
        public required Histogram<long> ProcessingTime { get; init; }

        public static MetricSet Create(string prefix) => new()
        {
            TokenCount = AbbotTelemetry.Meter.CreateHistogram<int>($"{prefix}.tokens", "tokens"),
            Duration = AbbotTelemetry.Meter.CreateHistogram<long>($"{prefix}.duration", "milliseconds"),
            ProcessingTime = AbbotTelemetry.Meter.CreateHistogram<long>($"{prefix}.processing-time", "milliseconds"),
        };
    }

    public OpenAIClient(IOptions<OpenAIOptions> openAiOptions)
    {
        _openAiOptions = openAiOptions.Value;
        _chatMetrics = MetricSet.Create("openai.chat");
        _completionMetrics = MetricSet.Create("openai.completion");
        _embeddingsMetrics = MetricSet.Create("openai.embeddings");
    }

    public async Task<ChatResult> GetChatResultAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        double temperature,
        Member actor)
    {
        if (!IsChatGptModel(model))
        {
            throw new ArgumentException("This feature requires a gpt-* model");
        }

        var openApi = CreateApiClient();

        var request = new ChatRequest
        {
            Messages = messages.ToList(),
            Model = model,
            MaxTokens = _openAiOptions.MaxTokens ?? 1000,
            user = $"Member:{actor.Id}",
            Temperature = temperature,
        };

        // Generate a response to the question
        var stopwatch = Stopwatch.StartNew();
        var response = await openApi.Chat.CreateChatCompletionAsync(request);
        // Only track non-exception responses to avoid skewing metrics when issues like the network is down, etc. occur.
        TrackMetrics(_chatMetrics, stopwatch, response, response.Usage, actor.Organization);
        return response;
    }

    public async Task<CompletionResult?> GetCompletionAsync(
        string prompt,
        string model,
        double temperature,
        Member actor,
        CancellationToken cancellationToken = default)
    {
        if (IsChatGptModel(model))
        {
            throw new ArgumentException("This feature does not support a gpt-* model");
        }

        var openApi = CreateApiClient();

        var completionRequest = new CompletionRequest
        {
            Prompt = prompt,
            Model = model,
            MaxTokens = _openAiOptions.MaxTokens ?? 1000,
            user = $"Member:{actor.Id}",
            Temperature = temperature,
        };

        // Generate a response to the question
        var stopwatch = Stopwatch.StartNew();
        var response = await openApi.Completions.CreateCompletionAsync(completionRequest);
        // Only track non-exception responses to avoid skewing metrics when issues like the network is down, etc. occur.
        TrackMetrics(_completionMetrics, stopwatch, response, response.Usage, actor.Organization);
        return response;
    }

    public async Task<EmbeddingResult> CreateEmbeddingsAsync(string input, string model, Organization organization)
    {
        var request = new EmbeddingRequest
        {
            Input = input,
            Model = model
        };
        var openApi = CreateApiClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await openApi.Embeddings.CreateEmbeddingAsync(request);

        // Only track non-exception responses to avoid skewing metrics when issues like the network is down, etc. occur.
        TrackMetrics(_embeddingsMetrics, stopwatch, response, response.Usage, organization);

        return response;
    }

    public async Task<IReadOnlyList<Model>> GetModelsAsync()
    {
        var openApiAuth = new APIAuthentication(_openAiOptions.ApiKey.Require(), _openAiOptions.OrganizationId);
        var openApi = new OpenAIAPI(openApiAuth);

        return await openApi.Models.GetModelsAsync();
    }

    static void TrackMetrics(MetricSet metrics, Stopwatch stopwatch, ApiResultBase response, Usage usage, Organization organization)
    {
        var metricTags = AbbotTelemetry.CreateOrganizationTags(organization);
        metrics.TokenCount.Record(usage.TotalTokens, metricTags);

        metricTags.Add("TokenCountBucket", GetTokenCountBucket(usage.TotalTokens));
        metrics.Duration.Record(stopwatch.ElapsedMilliseconds, metricTags);
        metrics.ProcessingTime.Record((long)response.ProcessingTime.TotalMilliseconds, metricTags);
    }

    OpenAIAPI CreateApiClient()
        => new(new APIAuthentication(_openAiOptions.ApiKey.Require(), _openAiOptions.OrganizationId));

    public static bool IsChatGptModel(string model) => model.StartsWith("gpt-", StringComparison.Ordinal);

    /// <summary>
    /// Returns the token count for the model (as best as we know)
    /// </summary>
    /// <param name="model">The model</param>
    /// <returns></returns>
    public static int TokenCount(string model) =>
        model switch
        {
            "gpt-4" or "gpt-4-0314" => 8192,
            "gpt-4-32k" or "gpt-4-32k-0314" => 32768,
            "gpt-3.5-turbo"
                or "gpt-3.5-turbo-0301"
                or "text-davinci-003"
                or "text-davinci-002" => 4097,
            "code-davinci-001" or "code-davinci-002" => 8001,
            "text-curie-001"
                or "text-babbage-001"
                or "text-ada-001"
                or "davinci"
                or "curie"
                or "babbage"
                or "ada" => 2049,
            "code-cushman-001" or "code-cushman-002" => 2048,
            // We'll fix these as new models come out, but we can at least make a best guess for forward
            // compatibility.
            _ => model.StartsWith("gpt-4", StringComparison.Ordinal)
                ? 8192
                : model.StartsWith("gpt-5", StringComparison.Ordinal)
                    ? 8192
                : 4097 // Best guess
        };

    static string GetTokenCountBucket(int actualTokenCount)
    {
        // Metric dimensions need to have fairly low "cardinality" (number of unique values) to be useful.
        // This is a simple bucketing algorithm to reduce the number of unique values.
        return actualTokenCount switch
        {
            1 => "1",
            <= 10 => "2-10",
            <= 100 => "11-100",
            <= 1000 => "101-1000",
            <= 10000 => "1001-10000",
            _ => "10001+"
        };
    }
}
