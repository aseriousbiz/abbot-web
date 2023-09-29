using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Triggers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Web;
using Serious.AspNetCore;

namespace Serious.Abbot.Pages.Playbooks;

[FeatureGate(FeatureFlags.Playbooks)]
public class ViewPlaybookPage : StaffViewablePage
{
    public DomId PlaybookLastPublishedStatusDomId { get; } = new("playbook-last-published-status");
    public DomId PlaybookLastRunStatusDomId { get; } = new("playbook-last-run-status");
    public DomId PlaybookManualTriggerDomId { get; } = new("playbook-manual-trigger");
    public DomId PlaybookPublishButtonDomId { get; } = new("playbook-publish-button");

    readonly PlaybookRepository _repository;
    readonly PlaybookPublisher _playbookPublisher;
    readonly PlaybookDispatcher _playbookDispatcher;
    readonly StepTypeCatalog _stepTypeCatalog;
    readonly IUrlGenerator _urlGenerator;
    readonly FeatureService _featureService;
    readonly ILogger<ViewPlaybookPage> _logger;
    Playbook? _playbook;

    public IReadOnlyList<string> ActiveFeatureFlags { get; private set; } = null!;

    [BindProperty]
    public PlaybookVersionInputModel Input { get; set; } = new();

    public PlaybookViewModel Playbook { get; private set; } = null!;

    public Uri WebhookTriggerUrl { get; private set; } = null!;

    public bool HasManualTrigger { get; private set; }
    public DispatchType? ManualDispatchType { get; private set; }
    public bool ManualTriggerEnabled { get; private set; }
    public string? ManualTriggerTooltip { get; private set; }

    PlaybookDefinition? PublishedPlaybookDefinition { get; set; }

    public ViewPlaybookPage(
        PlaybookRepository repository,
        PlaybookPublisher playbookPublisher,
        PlaybookDispatcher playbookDispatcher,
        StepTypeCatalog stepTypeCatalog,
        IUrlGenerator urlGenerator,
        FeatureService featureService,
        ILogger<ViewPlaybookPage> logger)
    {
        _repository = repository;
        _playbookPublisher = playbookPublisher;
        _playbookDispatcher = playbookDispatcher;
        _stepTypeCatalog = stepTypeCatalog;
        _urlGenerator = urlGenerator;
        _featureService = featureService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        if (await InitializePageAsync(slug) is { } result)
        {
            return result;
        }

        using var playbookScope = _logger.BeginPlaybookScope(_playbook);

        var playbookVersion = Playbook.CurrentVersion;
        if (playbookVersion is not null)
        {
            Input.Definition = playbookVersion.SerializedDefinition;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string slug, bool publish = false)
    {
        if (await InitializePageAsync(slug) is { } result)
        {
            return result;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        using var playbookScope = _logger.BeginPlaybookScope(_playbook);

        // Validate the playbook
        var playbookDefinition = PlaybookFormat.Deserialize(Input.Definition);
        if (PlaybookFormat.Validate(playbookDefinition) is { Count: > 0 } errors)
        {
            // The editor should prevent any actual invalid playbooks from being submitted, so we don't need fancy errors here.
            return TurboFlash($"The playbook is invalid: {string.Join(", ", errors.Select(e => e.Message))}.", isError: true);
        }

        // Don't allow publishing with steps that require missing integrations.
        if (publish)
        {
            var missingIntegrations = await CheckMissingIntegrationsAsync(playbookDefinition);
            if (missingIntegrations.Any())
            {
                var integrationNames = missingIntegrations
                    .Select(i => i switch {
                        // Yes, this is a hack, but it'll do for now.
                        IntegrationType.SlackApp => "Slack",
                        _ => i.ToString(),
                    });
                return TurboFlash(
                    $"The playbook depends on integrations that are not enabled in this environment: {string.Join(", ", integrationNames)}.",
                    isError: true);
            }
        }

        try
        {
            var editingVersion = await _repository.UpdateLatestVersionAsync(_playbook!, Input.Definition, Viewer);
            var publishedVersion = publish
                ? await _playbookPublisher.PublishAsync(_playbook!, Viewer)
                : Playbook.PublishedVersion;

            Playbook = PlaybookViewModel.FromPlaybook(
                _playbook!,
                editingVersion,
                publishedVersion,
                Playbook.LastRunGroup);
            InitializeFromPlaybook();

            return RedirectOrUpdatePage(
                publish ? $"Published Playbook version {editingVersion.Version}" : null);
        }
        catch (Exception e)
        {
            return TurboFlash($"{WebConstants.ErrorStatusPrefix} Failed to create playbook version: {e.Message}");
        }
    }

    public async Task<IActionResult> OnPostSetEnabledAsync(string slug, bool enabled)
    {
        if (await InitializePageAsync(slug) is { } result)
        {
            return result;
        }

        using var playbookScope = _logger.BeginPlaybookScope(_playbook);

        await _playbookPublisher.SetPlaybookEnabledAsync(_playbook!, enabled, Viewer);
        InitializeFromPlaybook();

        return RedirectOrUpdatePage();
    }

    private IActionResult RedirectOrUpdatePage(string? flashMessage = null)
    {
        if (!Request.IsTurboRequest())
        {
            StatusMessage = flashMessage;
            return RedirectToPage();
        }

        return TurboStream(
                TurboUpdate(PlaybookLastPublishedStatusDomId, Partial("_LastPublishedStatus", Playbook)),
                TurboUpdate(PlaybookLastRunStatusDomId, Partial("_LastRunStatus", Playbook)),
                TurboUpdate(PlaybookManualTriggerDomId, Partial("_ManualTrigger", this)),
                TurboUpdate(PlaybookPublishButtonDomId, Partial("_PublishButton", Playbook)),
                flashMessage is null ? null : TurboFlash(flashMessage));
    }

    public async Task<IActionResult> OnPostRunPlaybookAsync(string slug)
    {
        var result = await InitializePageAsync(slug);
        if (result is not null)
        {
            return result;
        }

        if (!Playbook.Enabled)
        {
            return TurboFlash("Cannot trigger a disabled Playbook.");
        }

        var manualTrigger = PublishedPlaybookDefinition?.Triggers.FirstOrDefault(t => t.Type == ManualTrigger.Id);
        if (manualTrigger is not null && Playbook.PublishedVersion is { } publishedVersion)
        {
            await _playbookDispatcher.DispatchAsync(
                publishedVersion,
                manualTrigger.Type,
                new Dictionary<string, object?>(),
                actor: Viewer);
            return TurboFlash("Playbook run manually triggered.");
        }

        return TurboFlash("Could not trigger a Playbook run.");
    }

    async Task<IActionResult?> InitializePageAsync(string slug)
    {
        _playbook = await _repository.GetBySlugAsync(slug, Organization);
        if (_playbook is null)
        {
            {
                return NotFound();
            }
        }

        // Find active feature flags.
        var allFeatures = await _featureService.GetFeatureStateAsync(Viewer);
        ActiveFeatureFlags = allFeatures.Where(f => f.Enabled).Select(f => f.Flag).ToList();

        WebhookTriggerUrl = _urlGenerator.GetPlaybookWebhookTriggerUrl(_playbook);

        var editingVersion = await _repository.GetCurrentVersionAsync(_playbook, includeDraft: true, includeDisabled: true);
        var publishedVersion = await _repository.GetCurrentVersionAsync(_playbook, includeDraft: false, includeDisabled: true);
        var lastRunGroup = await _repository.GetLatestRunGroupAsync(_playbook);
        Playbook = PlaybookViewModel.FromPlaybook(_playbook, editingVersion, publishedVersion, lastRunGroup);
        InitializeFromPlaybook();
        return null;
    }

    void InitializeFromPlaybook()
    {
        Expect.NotNull(_playbook);

        var publishedVersion = Playbook.PublishedVersion;
        PublishedPlaybookDefinition = PlaybookFormat.Deserialize(publishedVersion?.SerializedDefinition);
        ManualTriggerEnabled = DefinitionHasManualTrigger(PublishedPlaybookDefinition);

        var publishedDispatch = PublishedPlaybookDefinition?.Dispatch;
        ManualDispatchType = publishedDispatch?.Type;

        var editingVersion = Playbook.CurrentVersion;
        var editingPlaybookDefinition = PlaybookFormat.Deserialize(editingVersion?.SerializedDefinition);
        var editingDispatch = editingPlaybookDefinition?.Dispatch;

        // Start showing the button as soon as we've saved with one
        HasManualTrigger = ManualTriggerEnabled || DefinitionHasManualTrigger(editingPlaybookDefinition);
        (ManualTriggerEnabled, ManualTriggerTooltip) = !HasManualTrigger
            ? (false, "Run Playbook Button trigger does not exist") // Button should be hidden
            : !ManualTriggerEnabled
            ? (false, "Run Playbook Button trigger has not been published")
            : InStaffTools
            ? (false, "This view is read-only")
            : !_playbook.Enabled
            ? (false, "Cannot run a disabled Playbook")
            : publishedDispatch != editingDispatch
            ? (true, $"""
                         ⚠️ Dispatching update has not been published
                         Published: {publishedDispatch}
                         """)
            : (true, $"Run this Playbook: {publishedDispatch}");

        bool DefinitionHasManualTrigger(PlaybookDefinition? definition) =>
            definition?.Triggers.Any(t => t.Type == ManualTrigger.Id) == true;
    }

    async Task<HashSet<IntegrationType>> CheckMissingIntegrationsAsync(PlaybookDefinition playbookDefinition)
    {
        var stepTypeList = await _stepTypeCatalog.GetAllTypesAsync(Organization, HttpContext.GetFeatureActor());

        // Check if the playbook depends on any steps that are not available in this environment
        var missingIntegrations = new HashSet<IntegrationType>();
        foreach (var step in playbookDefinition.EnumerateAllSteps())
        {
            if (!_stepTypeCatalog.TryGetType(step.Type, out var stepType))
            {
                continue;
            }

            var stepMissing = stepType.RequiredIntegrations
                .Where(i => !stepTypeList.EnabledIntegrations.Contains(i));
            missingIntegrations.AddRange(stepMissing);
        }

        return missingIntegrations;
    }
}

public class PlaybookVersionInputModel
{
    public string Definition { get; set; } = null!;
}
