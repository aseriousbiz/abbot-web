using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refit;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Integrations.HubSpot.Models;
using Serious.Abbot.Repositories;
using Serious.AspNetCore;
using Serious.Logging;

namespace Serious.Abbot.Pages.Settings.Organization.Integrations.HubSpot;

public class ConfigureModel : HubSpotPageBase
{
    static readonly ILogger<ConfigureModel> Log = ApplicationLoggerFactory.CreateLogger<ConfigureModel>();
    readonly IHubSpotClientFactory _hubSpotClientFactory;

    public static readonly DomId FormId = new("pipeline-select-form");

    public ConfigureModel(IIntegrationRepository integrationRepository, IOptions<HubSpotOptions> hubSpotOptions, IHubSpotClientFactory hubSpotClientFactory) : base(integrationRepository, hubSpotOptions)
    {
        _hubSpotClientFactory = hubSpotClientFactory;
    }

    public IReadOnlyList<TicketPipeline> Pipelines { get; private set; } = Array.Empty<TicketPipeline>();

    public IReadOnlyList<SelectListItem> AvailablePipelines { get; set; } = Array.Empty<SelectListItem>();

    [Required]
    [BindProperty]
    [Display(Name = "Default Ticket Pipeline")]
    public string? SelectedPipelineId { get; set; }

    public TicketPipeline? SelectedPipeline { get; set; }

    public IReadOnlyList<SelectListItem> AvailableStages { get; set; } = Array.Empty<SelectListItem>();

    [Required]
    [BindProperty]
    [Display(Name = "Pipeline Stage: New")]
    public string? SelectedStageIdNew { get; set; }

    [BindProperty]
    [Display(Name = "Pipeline Stage: Waiting")]
    public string? SelectedStageIdWaiting { get; set; }

    [BindProperty]
    [Display(Name = "Pipeline Stage: Needs Response")]
    public string? SelectedStageIdNeedsResponse { get; set; }

    [BindProperty]
    [Display(Name = "Pipeline Stage: Closed")]
    public string? SelectedStageIdClosed { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Settings.HasApiCredentials)
        {
            return RedirectWithStatusMessage("The HubSpot integration is not installed yet.",
                "/Settings/Organization/Integrations/HubSpot/Index");
        }

        SelectedPipelineId = Settings.TicketPipelineId;
        SelectedStageIdNew = Settings.NewTicketPipelineStageId;
        SelectedStageIdWaiting = Settings.WaitingTicketPipelineStageId;
        SelectedStageIdNeedsResponse = Settings.NeedsResponseTicketPipelineStageId;
        SelectedStageIdClosed = Settings.ClosedTicketPipelineStageId;

        if (await InitializeAsync() is { } result)
        {
            return result;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSelectPipelineAsync()
    {
        // Ignore selected stage id validation errors.
        if (ModelState.TryGetValue(nameof(SelectedStageIdNew), out var selectedStageIdNewState))
        {
            selectedStageIdNewState.ValidationState = ModelValidationState.Skipped;
        }

        if (ModelState.IsValid && await InitializeAsync() is { } result)
        {
            return result;
        }

        return TurboReplaceOrPage();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await InitializeAsync() is { } result)
        {
            return result;
        }

        // Required because InitializeAsync() should have already validated
        Settings.TicketPipelineId = SelectedPipeline.Require().Id;
        Settings.NewTicketPipelineStageId = SelectedStageIdNew.Require();

        // Optional
        Settings.WaitingTicketPipelineStageId = SelectedStageIdWaiting;
        Settings.NeedsResponseTicketPipelineStageId = SelectedStageIdNeedsResponse;
        Settings.ClosedTicketPipelineStageId = SelectedStageIdClosed;

        await IntegrationRepository.SaveSettingsAsync(Integration, Settings);
        return RedirectWithStatusMessage("Settings saved.", "/Settings/Organization/Integrations/HubSpot/Index");
    }

    async Task<IActionResult?> InitializeAsync()
    {
        // We need to fetch available pipelines (and their stages, if a pipeline is selected).
        var client = await _hubSpotClientFactory.CreateClientAsync(Integration, Settings);
        try
        {
            var result = await client.GetTicketPipelinesAsync();
            Pipelines = result.Results;
        }
        catch (ApiException apiex) when (apiex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return RedirectWithStatusMessage("Your HubSpot credentials are invalid. Please re-install the integration.",
                "/Settings/Organization/Integrations/HubSpot/Index");
        }
        catch (Exception ex)
        {
            Log.ErrorFetchingPipelines(ex);
            return RedirectWithStatusMessage(
                $"I couldn't fetch pipelines due to an error at HubSpot. {WebConstants.GetContactSupportSentence()}",
                "/Settings/Organization/Integrations/HubSpot/Index");
        }

        AvailablePipelines = Pipelines
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new SelectListItem(p.Label, p.Id))
            .Prepend(new SelectListItem("", ""))
            .ToList();

        if (SelectedPipelineId is { Length: > 0 })
        {
            SelectedPipeline = Pipelines.FirstOrDefault(p => p.Id == SelectedPipelineId);
            if (SelectedPipeline is null)
            {
                ModelState.AddModelError(nameof(SelectedPipelineId), "The selected pipeline is not available.");
                return TurboReplaceOrPage();
            }

            AvailableStages = SelectedPipeline.Stages
                .OrderBy(s => s.DisplayOrder)
                .Select(s => new SelectListItem(s.Label, s.Id))
                .Prepend(new SelectListItem("", ""))
                .ToList();

            if (SelectedStageIdNew is { Length: > 0 }
                && !SelectedPipeline.Stages.Any(p => p.Id == SelectedStageIdNew))
            {
                SelectedStageIdNew = null;
                ModelState.AddModelError(nameof(SelectedStageIdNew), "The selected pipeline stage is not available.");
            }

            if (SelectedStageIdWaiting is { Length: > 0 }
                && !SelectedPipeline.Stages.Any(p => p.Id == SelectedStageIdWaiting))
            {
                SelectedStageIdWaiting = null;
                ModelState.AddModelError(nameof(SelectedStageIdWaiting), "The selected pipeline stage is not available.");
            }

            if (SelectedStageIdNeedsResponse is { Length: > 0 }
                && !SelectedPipeline.Stages.Any(p => p.Id == SelectedStageIdNeedsResponse))
            {
                SelectedStageIdNeedsResponse = null;
                ModelState.AddModelError(nameof(SelectedStageIdNeedsResponse), "The selected pipeline stage is not available.");
            }

            if (SelectedStageIdClosed is { Length: > 0 }
                && !SelectedPipeline.Stages.Any(p => p.Id == SelectedStageIdClosed))
            {
                SelectedStageIdClosed = null;
                ModelState.AddModelError(nameof(SelectedStageIdClosed), "The selected pipeline stage is not available.");
            }
        }

        return ModelState.IsValid ? null : TurboReplaceOrPage();
    }

    private IActionResult TurboReplaceOrPage() =>
        Request.IsTurboRequest()
            ? TurboReplace(FormId, Partial("_PipelineSelectionForm", this))
            : Page();
}

public static partial class HubSpotConfigurePageLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message =
            "Error fetching pipelines from HubSpot.")]
    public static partial void ErrorFetchingPipelines(this ILogger<ConfigureModel> logger, Exception ex);
}
