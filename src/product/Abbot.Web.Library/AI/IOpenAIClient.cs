using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Embedding;
using OpenAI_API.Models;
using Serious.Abbot.Entities;
using Serious.Logging;

namespace Serious.Abbot.AI;

/// <summary>
/// Abstracts the OpenAI API.
/// </summary>
public interface IOpenAIClient
{
    static readonly ILogger<IOpenAIClient> Log = ApplicationLoggerFactory.CreateLogger<IOpenAIClient>();
    static readonly ISensitiveLogDataProtector DataProtector = ApplicationLoggerFactory.DataProtector;

    /// <summary>
    /// Retrieves completions from the OpenAI Completions API given the message and member.
    /// </summary>
    /// <param name="prompt">The prompt to send to Chat GPT.</param>
    /// <param name="model">The model to use for the completions.</param>
    /// <param name="temperature">A number between 0 and 1 that controls how much randomness is in the output.</param>
    /// <param name="actor">The user sending the prompt.</param>
    /// <param name="cancellationToken"></param>
    Task<CompletionResult?> GetCompletionAsync(
        string prompt,
        string model,
        double temperature,
        Member actor,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="GetCompletionAsync(string, string, double, Member, CancellationToken)"/>
    async Task<CompletionResult?> SafelyGetCompletionAsync(
        string prompt,
        string model,
        double temperature,
        Member actor,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetCompletionAsync(prompt, model, temperature, actor, cancellationToken);
        }
        catch (Exception ex)
        {
            var protectedPrompt = DataProtector.Protect(prompt);
            Log.RequestFailed(ex, model, temperature, protectedPrompt);
            return null;
        }
    }

    /// <summary>
    /// Retrieves completions from the OpenAI Chat API given the message and member.
    /// </summary>
    /// <param name="messages">The messages to send to Chat GPT.</param>
    /// <param name="model">The model to use for the completions.</param>
    /// <param name="temperature">A number between 0 and 1 that controls how much randomness is in the output.</param>
    /// <param name="actor">The user sending the prompt.</param>
    Task<ChatResult> GetChatResultAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        double temperature,
        Member actor);

    /// <inheritdoc cref="GetChatResultAsync(IEnumerable{ChatMessage}, string, double, Member)"/>
    async Task<ChatResult?> SafelyGetChatResultAsync(
        IEnumerable<ChatMessage> messages,
        string model,
        double temperature,
        Member actor)
    {
        try
        {
            return await GetChatResultAsync(messages, model, temperature, actor);
        }
        catch (Exception ex)
        {
            var protectedPrompt = DataProtector.Protect(messages.Format());
            Log.RequestFailed(ex, model, temperature, protectedPrompt);
            return null;
        }
    }

    /// <summary>
    /// Retrieves the set of available models.
    /// </summary>
    /// <returns></returns>
    Task<IReadOnlyList<Model>> GetModelsAsync();

    /// <summary>
    /// Creates embeddings for the provided text using the OpenAI GPT-3 API.
    /// </summary>
    /// <param name="input">The text to create embeddings for.</param>
    /// <param name="model">The name of the model to use for the embeddings.</param>
    /// <param name="organization">The <see cref="Organization"/> in which the embeddings are being generated.</param>
    Task<EmbeddingResult> CreateEmbeddingsAsync(string input, string model, Organization organization);
}

static partial class OpenAIClientLoggerExtensions
{
    [LoggerMessage(
        EventId = 4141,
        Level = LogLevel.Warning,
        Message = "OpenAI request failed. (Model={AIModel}, Temp={AITemperature}) {ProtectedPrompt}")]
    public static partial void RequestFailed(
        this ILogger logger,
        Exception ex,
        string aiModel,
        double aiTemperature,
        string protectedPrompt);
}
