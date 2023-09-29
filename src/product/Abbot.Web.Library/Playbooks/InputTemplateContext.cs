using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// The context provided when evaluating step input templates.
/// </summary>
public record InputTemplateContext
{
    /// <summary>
    /// A dictionary of results from all steps that have already executed in the playbook.
    /// </summary>
    public required IDictionary<string, TemplateStepResult> Steps { get; init; }

    /// <summary>
    /// The result of the step that executed immediately before this one.
    /// </summary>
    public required TemplateStepResult? Previous { get; init; }

    /// <summary>
    /// The result of the step that triggered this playbook.
    /// </summary>
    public required TemplateStepResult? Trigger { get; init; }

    /// <summary>
    /// A merged list of outputs from all steps.
    /// Outputs from later steps will overwrite any outputs from earlier steps with the same name.
    /// </summary>
    public required IDictionary<string, object?> Outputs { get; init; }
}

public record TemplateStepResult
{
    /// <summary>
    /// The ID of the step.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The <see cref="StepOutcome"/> describing the result of the step.
    /// </summary>
    public required StepOutcome Outcome { get; init; }

    /// <summary>
    /// The outputs from the step.
    /// </summary>
    public required IDictionary<string, object?> Outputs { get; init; }

    /// <summary>
    /// A <see cref="ProblemDetails"/> describing the error that occurred, if any.
    /// </summary>
    /// <remarks>
    /// We don't currently provide a "Catch" or "Continue on Error" behavior to allow for error handling.
    /// But we may want to in the future, and this would store the error details for a step to be accessed by later steps.
    /// </remarks>
    public required ProblemDetails? Problem { get; init; }
}
