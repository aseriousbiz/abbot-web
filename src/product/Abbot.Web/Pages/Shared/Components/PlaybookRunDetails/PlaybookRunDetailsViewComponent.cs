using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Azure;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Playbooks.Runs;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Shared.Components.PlaybookRunDetails;

public class PlaybookRunDetailsViewComponent : ViewComponent
{
    static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };
    readonly StepTypeCatalog _stepTypeCatalog;
    readonly IUserRepository _userRepository;

    public PlaybookRunDetailsViewComponent(StepTypeCatalog stepTypeCatalog, IUserRepository userRepository)
    {
        _stepTypeCatalog = stepTypeCatalog;
        _userRepository = userRepository;
    }

    public async Task<IViewComponentResult> InvokeAsync(PlaybookRun run)
    {
        var definition = PlaybookFormat.Deserialize(run.SerializedDefinition);
        var completedActions = run.Properties.CompletedSteps.Select(s =>
            CreateStepModel(definition.TryGetAction(s, out var a)
                ? a
                : throw new UnreachableException(
                    $"Playbook State references a step '{s.ActionId}' that does not exist."), run))
            .ToList();

        var model = new ViewModel()
        {
            Run = run,
            CompletedActions = completedActions,
            Abbot = await _userRepository.EnsureAbbotMemberAsync(run.Playbook.Organization),
        };

        model.Canceller = run.Properties.CancellationRequestedBy is not null
            ? await _userRepository.GetMemberByIdAsync(run.Properties.CancellationRequestedBy.Value)
            : null;

        // Pull the trigger out, if we can
        if (run.Properties.Trigger is { Length: > 0 } stepId)
        {
            var step = definition.Triggers.FirstOrDefault(t => t.Id == stepId)
                .Require($"Playbook State references a step '{stepId}' that does not exist.");
            model.Trigger = CreateStepModel(step, run);
        }

        model.Dispatch = run.Properties.DispatchContext;

        // Don't set ActiveAction is result is non-null.
        // If the result is non-null, then the playbook is done and the active step just represents the last step.
        if (run.Properties.Result is null && run.Properties.ActiveStep is { } activeStepReference)
        {
            var activeStep = definition.TryGetAction(activeStepReference, out var a)
                ? a
                : throw new UnreachableException(
                    $"Playbook State references a step '{activeStepReference.ActionId}' that does not exist.");
            model.ActiveAction = CreateStepModel(activeStep, run);
        }

        if (HttpContext.IsStaffMode())
        {
            model.FormattedState = JsonSerializer.Serialize(
                run.Properties,
                JsonSerializerOptions);

            // Just in case the definition is invalid, let's just reformat the original
            model.FormattedDefinition = JsonNode.Parse(run.SerializedDefinition)
                .Require()
                .ToJsonString(JsonSerializerOptions);

            model.FormattedProblem = run.Properties.Result is { Problem: { } problem }
                ? JsonSerializer.Serialize(problem, JsonSerializerOptions)
                : null;
        }

        return View(model);
    }

    StepViewModel CreateStepModel(Step step, PlaybookRun run)
    {
        var stepModel = new StepViewModel()
        {
            Step = step,
            Type = _stepTypeCatalog.TryGetType(step.Type, out var t) ? t : null,
            Result = run.Properties.StepResults.TryGetValue(step.Id, out var r) ? r : null,
        };
        return stepModel;
    }

    public record ViewModel
    {
        public required PlaybookRun Run { get; init; }
        public required IList<StepViewModel> CompletedActions { get; init; }
        public required Member Abbot { get; set; }
        public StepViewModel? Trigger { get; set; }
        public DispatchContext? Dispatch { get; set; }
        public StepViewModel? ActiveAction { get; set; }
        public string? FormattedState { get; set; }
        public string? FormattedDefinition { get; set; }
        public string? FormattedProblem { get; set; }
        public Member? Canceller { get; set; }
    }

    public record StepViewModel
    {
        public required Step Step { get; set; }
        public required StepType? Type { get; set; }
        public required StepResult? Result { get; set; }
    }
}
