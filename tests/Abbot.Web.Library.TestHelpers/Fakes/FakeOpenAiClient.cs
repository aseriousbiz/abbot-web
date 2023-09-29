using AI.Dev.OpenAI.GPT;
using OpenAI_API.Chat;
using OpenAI_API.Completions;
using OpenAI_API.Embedding;
using OpenAI_API.Models;
using Serious;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Commands;
using Serious.Abbot.Entities;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeOpenAiClient : IAzureOpenAIClient
{
    readonly Stack<(string?, Exception?)> _chatResults = new();
    readonly Stack<(string?, Exception?)> _completionResults = new();
    readonly List<IReadOnlyList<ChatMessage>> _receivedChatPrompts = new();
    readonly List<string> _receivedPrompts = new();
    readonly List<(string Input, string Model)> _receivedEmbeddings = new();
    readonly Stack<float[]> _embeddingResults = new();

    readonly IReadOnlyList<Model> _models = new Model[]
    {
        new("Ada"),
        new("Curie"),
        new("DaVinci")
    };

    public void PushCompletionResult(string result) => _completionResults.Push((result, null));

    public void PushCompletionResult(Exception exception) => _completionResults.Push((null, exception));

    public void PushChatResult(string result) => _chatResults.Push((result, null));

    public void PushChatResult(Reasoned<string> reasonedAction) => _chatResults.Push((reasonedAction.ToString(), null));

    public void PushChatResult(Reasoned<Command> reasonedAction) =>
        PushChatResult(reasonedAction.Map(c => new CommandList(new[] { c })));

    public void PushChatResult(Reasoned<CommandList> reasonedAction) =>
        PushChatResult(reasonedAction.ToString());

    public void PushChatResult(Exception exception) => _chatResults.Push((null, exception));

    public Task<CompletionResult?> GetCompletionAsync(string prompt, string model, double temperature, Member actor, CancellationToken cancellationToken)
    {
        _receivedPrompts.Add(prompt);
        var promptTokenCount = GPT3Tokenizer.Encode(prompt).Count;

        var summary = _completionResults.TryPop(out var result)
            ? result switch
            {
                (_, { } ex) => throw ex,
                (var text, _) => text.Require(),
            }
            : "The summary";

        var completionTokenCount = (short)GPT3Tokenizer.Encode(summary).Count;

        var completionResult = new CompletionResult
        {
            Completions = new List<Choice> { new() { Text = summary } },
            Usage = new CompletionUsage
            {
                PromptTokens = promptTokenCount,
                CompletionTokens = completionTokenCount,
                TotalTokens = promptTokenCount + completionTokenCount,
            }
        };

        return Task.FromResult<CompletionResult?>(completionResult);
    }

    public Task<ChatResult> GetChatResultAsync(IEnumerable<ChatMessage> messages, string model, double temperature, Member actor)
    {
        var messageList = messages.ToList();
        _receivedChatPrompts.Add(messageList);
        var promptTokenCount = messageList.Sum(m => GPT3Tokenizer.Encode(m.Content).Count);

        var summary = _chatResults.TryPop(out var result)
            ? result switch
            {
                (_, { } ex) => throw ex,
                (var text, _) => text.Require(),
            }
            : "The summary";

        var completionTokenCount = GPT3Tokenizer.Encode(summary).Count;
        var chatResult = new ChatResult
        {
            Choices = new List<ChatChoice> { new() { Message = new ChatMessage(ChatMessageRole.Assistant, summary) } },
            Usage = new ChatUsage
            {
                PromptTokens = promptTokenCount,
                CompletionTokens = completionTokenCount,
                TotalTokens = promptTokenCount + completionTokenCount,
            }
        };

        return Task.FromResult(chatResult);
    }

    public Task<IReadOnlyList<Model>> GetModelsAsync() => Task.FromResult(_models);

    public Task<EmbeddingResult> CreateEmbeddingsAsync(string input, string model, Organization organization)
    {
        _receivedEmbeddings.Add((input, model));

        var embedding = _embeddingResults.TryPop(out var r)
            ? r
            : new[] { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f };
        return Task.FromResult(new EmbeddingResult
        {
            Data = new List<Data>
            {
                new()
                {
                    Embedding = embedding
                }
            }
        });
    }

    public IReadOnlyList<string> ReceivedPrompts => _receivedPrompts;

    public IReadOnlyList<IReadOnlyList<ChatMessage>> ReceivedChatPrompts => _receivedChatPrompts;
    public IReadOnlyList<(string Input, string Model)> ReceivedEmbeddings => _receivedEmbeddings;

    public bool Enabled { get; set; }
}
