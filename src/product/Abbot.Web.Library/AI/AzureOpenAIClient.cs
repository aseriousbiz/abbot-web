using System.Collections.Generic;
using System.Threading;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Embedding;
using OpenAI_API.Models;
using Serious.Abbot.Entities;
using Serious.Logging;
using ChatMessage = OpenAI_API.Chat.ChatMessage;

namespace Serious.Abbot.AI;

public class AzureOpenAIClient : IAzureOpenAIClient
{
    static readonly ILogger<AzureOpenAIClient> Log = ApplicationLoggerFactory.CreateLogger<AzureOpenAIClient>();
    static readonly ISensitiveLogDataProtector DataProtector = ApplicationLoggerFactory.DataProtector;

    readonly AzureOpenAIOptions _options;

    public AzureOpenAIClient(IOptions<AzureOpenAIOptions> options)
    {
        _options = options.Value;
    }

    public bool Enabled => _options.Endpoint is not null;

    public async Task<CompletionResult?> GetCompletionAsync(
        string prompt,
        string model,
        double temperature,
        Member actor,
        CancellationToken cancellationToken = default)
    {
        if (model is not "text-davinci-003")
        {
            throw new InvalidOperationException("Right now, we only support the text-davinci-003 model.");
        }
        try
        {
            var endpoint = new Uri(_options.Endpoint.Require());
            var client = new Azure.AI.OpenAI.OpenAIClient(endpoint, new DefaultAzureCredential());
            var response = await client.GetCompletionsAsync(model, new CompletionsOptions
            {
                Temperature = (float)temperature,
                Prompts = { prompt },
                User = actor.User.PlatformUserId,
                MaxTokens = 2048,
            }, cancellationToken);

            return response.HasValue ? response.Value.ToCompletionResult() : null;
        }
        catch (Exception ex)
        {
            var protectedPrompt = DataProtector.Protect(prompt);
            Log.AzureOpenAIRequestFailed(ex, model, temperature, protectedPrompt);

            return null;
        }
    }

    public Task<ChatResult> GetChatResultAsync(IEnumerable<ChatMessage> messages, string model, double temperature, Member actor)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Model>> GetModelsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<EmbeddingResult> CreateEmbeddingsAsync(string input, string model, Organization organization)
    {
        throw new NotImplementedException();
    }
}

static partial class AzureOpenAIClientLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Azure OpenAI request failed. (Model={AIModel}, Temp={AITemperature}) {ProtectedPrompt}")]
    public static partial void AzureOpenAIRequestFailed(
        this ILogger<AzureOpenAIClient> logger,
        Exception ex,
        string aiModel,
        double aiTemperature,
        string protectedPrompt);
}
