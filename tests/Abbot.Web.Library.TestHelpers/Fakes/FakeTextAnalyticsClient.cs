using Azure.AI.TextAnalytics;
using Serious.Abbot.AI;
using Serious.Abbot.Clients;
using Serious.Abbot.Services;

namespace Serious.TestHelpers;

public class FakeTextAnalyticsClient : ITextAnalyticsClient
{
    readonly Dictionary<string, RedactResult> _results = new();

    public Task<RedactResult> RecognizePiiEntitiesAsync(
        string document,
        string? language = null,
        RecognizePiiEntitiesOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_results.TryGetValue(document, out var result)
            ? result
            : new RedactResult(document));
    }

    public void AddResult(string document, string redactedText, IEnumerable<SensitiveValue> sensitiveValues)
    {
        _results.Add(document, new RedactResult(redactedText, sensitiveValues.ToList()));
    }
}
