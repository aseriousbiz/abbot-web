using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.Abbot.AI;
using Serious.Abbot.Entities;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.Slack;

namespace Serious.Abbot.Pages.Staff.Tools;

public class AIPage : StaffToolsPage
{
    readonly IOpenAIClient _openAiClient;
    readonly Summarizer _summarizer;
    readonly IMessageClassifier _messageClassifier;
    readonly AISettingsRegistry _aiSettings;
    public DomId OpenAIResults { get; } = new("openai-results");

    public IReadOnlyList<SelectListItem> Features { get; private set; } = Array.Empty<SelectListItem>();

    public AIFeature? ActiveFeature { get; set; }

    [BindProperty]
    public ModelSettings? ModelSettings { get; set; }

    [BindProperty]
    public string? TestContent { get; set; }

    [BindProperty]
    public ConversationState ConversationState { get; set; } = ConversationState.NeedsResponse;

    public AIPage(
        IOpenAIClient openAiClient,
        Summarizer summarizer,
        IMessageClassifier messageClassifier,
        AISettingsRegistry aiSettings)
    {
        _openAiClient = openAiClient;
        _summarizer = summarizer;
        _messageClassifier = messageClassifier;
        _aiSettings = aiSettings;
    }

    public IReadOnlyList<SelectListItem> ModelsList { get; private set; } = Array.Empty<SelectListItem>();

    public async Task<IActionResult> OnGetAsync(string? feature)
    {
        if (!TryInitializeFeatures(feature))
        {
            return NotFound();
        }

        var models = await _openAiClient.GetModelsAsync();

        // Filter models to only those that are compatible with the active feature
        Func<string, bool> filter = ActiveFeature.UsesChatModel()
            ? OpenAIClient.IsChatGptModel
            : m => !OpenAIClient.IsChatGptModel(m);

        ModelsList = models.Select(m => m.ModelID)
            .Where(filter)
            .Order()
            .Select(m => new SelectListItem(m, m))
            .ToList();

        ModelSettings = ActiveFeature is not null
            ? await _aiSettings.GetModelSettingsAsync(ActiveFeature.Value)
            : null;

        return Page();
    }

    bool TryInitializeFeatures(string? feature)
    {
        if (feature is not null)
        {
            if (!Enum.TryParse<AIFeature>(feature, ignoreCase: true, out var f))
            {
                return false;
            }

            ActiveFeature = f;
        }

        var features = AISettingsRegistry.AllFeatures.Select(f => new SelectListItem(
            f.ToString(),
            Url.Page(null,
                null,
                new {
                    feature = f.ToString().ToLowerInvariant()
                }),
            ActiveFeature == f)).ToList();

        features.Insert(0,
            new SelectListItem("<None>",
                Url.Page(null,
                    null,
                    new {
                        feature = ""
                    }),
                ActiveFeature == null));

        Features = features;
        return true;
    }

    public async Task<IActionResult> OnPostAsync(string? feature)
    {
        if (!TryInitializeFeatures(feature))
        {
            return NotFound();
        }

        if (ActiveFeature is null)
        {
            return NotFound();
        }

        var existing = await _aiSettings.GetModelSettingsAsync(ActiveFeature.Value);

        await UpdateModelSettings(ModelSettings, existing, ActiveFeature.Value);

        return TurboFlash("Saved model changes");
    }

    async Task UpdateModelSettings(ModelSettings? newSettings, ModelSettings existingSettings, AIFeature feature)
    {
        if (newSettings != existingSettings)
        {
            if (newSettings is not null)
            {
                await _aiSettings.SetModelSettingsAsync(
                    feature,
                    newSettings,
                    Viewer);
            }
            else
            {
                await _aiSettings.ResetModelSettingsAsync(feature, Viewer);
            }
        }
    }

    public async Task<IActionResult> OnPostTestAsync(string? feature)
    {
        if (!TryInitializeFeatures(feature))
        {
            return NotFound();
        }

        if (ActiveFeature is null)
        {
            return NotFound();
        }

        if (TestContent is { Length: > 0 })
        {
            try
            {
                return ActiveFeature switch
                {
                    AIFeature.DefaultResponder => await TestDefaultResponderAsync(TestContent, ModelSettings.Require()),
                    AIFeature.Classifier => await TestClassifierAsync(TestContent, ModelSettings.Require()),
                    AIFeature.Summarization => await TestSummarizationAsync(TestContent, ModelSettings.Require()),
                    _ => TurboFlash($"Unknown feature: {feature}"),
                };
            }
            catch (Exception ex)
            {
                return TurboUpdate(OpenAIResults, ex.ToString());
            }
        }

        return TurboFlash("Type something.");
    }

    async Task<IActionResult> TestSummarizationAsync(string message, ModelSettings modelSettings)
    {
        var history = new SanitizedConversationHistory(
            new[]
            {
                new SourceMessage(message, new SourceUser(Viewer.User.PlatformUserId, "Agent"), SlackTimestamp.Parse("1676486431.793829"))
            },
            new Dictionary<string, SecretString>());

        var response = await _summarizer.SummarizeConversationAsync(
            history,
            new Conversation { State = ConversationState },
            Viewer,
            Viewer.Organization,
            modelSettings);
        if (response is null)
        {
            return TurboFlash("No response");
        }

        var directivesString = string.Join("", response.Directives.Select(d => $"<li><span class=\"font-semibold\">{d.Name}</span>: {d.RawArguments}</li>"));
        return TurboUpdate(OpenAIResults,
            $"""
            <li>
                <p><span class="font-semibold">Prompt</span>: {message}</p>
                <p><span class="font-semibold">Raw Prompt</span>:</p>
                <pre class="overflow-auto w-full">{response.Prompt.Reveal()}</pre>
                <p><span class="font-semibold">Summary</span>:</p>
                <pre class="overflow-auto w-full">{response.Summary}</pre>
                <p><span class="font-semibold">Directives</span>:</p>
                <ul class="list-disc">{directivesString}</ul>
            </li>
        """);
    }

    async Task<IActionResult> TestClassifierAsync(string message, ModelSettings modelSettings)
    {
        var response = await _messageClassifier.ClassifyMessageAsync(
            message,
            Array.Empty<SensitiveValue>(),
            "1676486431.793829",
            new Room { OrganizationId = Viewer.OrganizationId, Organization = Viewer.Organization },
            Viewer, Viewer.Organization, modelSettings);
        if (response is null)
        {
            return TurboFlash("No response");
        }

        var categories = response.Directives.Select(d => new Category(d.Name, d.RawArguments)).ToArray();
        var categoriesString = string.Join("", categories.Select(c => $"<li><code>{c}</code></li>"));
        return TurboUpdate(OpenAIResults,
            $"""
            <li>
                <p><span class="font-semibold">Content</span>: {message}</p>
                <p><span class="font-semibold">Raw Prompt</span>:</p>
                <pre class="overflow-auto w-full">{response.Prompt.Reveal()}</pre>
                <p><span class="font-semibold">Categories</span>:</p>
                <ul class="list-disc">{categoriesString}</ul>
            </li>
            """);
    }

    async Task<IActionResult> TestDefaultResponderAsync(string message, ModelSettings modelSettings)
    {
        var result = await _openAiClient.GetCompletionAsync(
            message,
            modelSettings.Model,
            modelSettings.Temperature,
            Viewer);

        return TurboUpdate(OpenAIResults,
            $"""
            <li>
                <p><span class="font-semibold">Prompt</span>: {message}</p>
                <p><span class="font-semibold">Response</span>:</p>
                <pre class="overflow-auto w-full">{result?.GetResultText()}</pre>
            </li>
            """);
    }
}
