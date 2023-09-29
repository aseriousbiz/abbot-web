using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Serious.Json;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Represents the result of executing a <see cref="Step"/> in a playbook.
/// </summary>
/// <param name="Outcome"></param>
public record StepResult(StepOutcome Outcome)
{
    /// <summary>
    /// Gets the computed inputs that led to this result
    /// </summary>
    [JsonConverter(typeof(DictionaryOfObjectsJsonConverter))]
    public IDictionary<string, object?>? Inputs { get; set; }

    /// <summary>
    /// Gets or inits a <see cref="ProblemDetails"/> describing the error that occurred, if any.
    /// </summary>
    public ProblemDetails? Problem { get; init; }

    /// <summary>
    /// Gets or inits a list of notices to be displayed to the user.
    /// </summary>
    public IReadOnlyList<Notice> Notices { get; init; } = new List<Notice>();

    /// <summary>
    /// Gets or inits the branch to call next.
    /// Only considered if <see cref="Outcome"/> is <see cref="StepOutcome.Succeeded"/>.
    /// </summary>
    /// <remarks>
    /// If this is non-<c>null</c>, the playbook will execute the named sequence next and then return to the normal flow.
    /// </remarks>
    public BranchReference? CallBranch { get; init; }

    /// <summary>
    /// The name of a partial in Pages/Shared/StepPresenters that will be rendered, with the <see cref="SuspendState"/> as the model,
    /// when displaying the status of the playbook while it is suspended at this step.
    /// </summary>
    public string? SuspendPresenter { get; init; }

    public DateTime? SuspendedUntil { get; set; }

    /// <summary>
    /// Gets or inits a dictionary of state to be persisted while the playbook is suspended.
    /// This state will be disregarded unless the outcome is <see cref="StepOutcome.Suspended"/>.
    /// </summary>
    [JsonConverter(typeof(DictionaryOfObjectsJsonConverter))]
    public IDictionary<string, object?> SuspendState { get; init; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets or inits a dictionary of outputs from this step.
    /// </summary>
    [JsonConverter(typeof(DictionaryOfObjectsJsonConverter))]
    public IDictionary<string, object?> Outputs { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// Provides context for a call to a branch associated with the step.
/// </summary>
/// <param name="BranchName">The name of the branch in the step type.</param>
/// <param name="SequenceName">The name of the sequence that will be run when this branch is taken.</param>
/// <param name="Description">A user-visible description of the branch that was taken.</param>
public record BranchReference(string BranchName, string? SequenceName, string Description);

/// <summary>
/// A notice to be displayed to the user.
/// </summary>
/// <param name="Type">The type of notice.</param>
/// <param name="Title">The title of the notice.</param>
/// <param name="Details">Details of the notice.</param>
public record Notice(NoticeType Type, string Title, string? Details = null);

/// <summary>
/// A type of notice.
/// </summary>
public enum NoticeType
{
    /// <summary>
    /// The notice is informational.
    /// </summary>
    Information,

    /// <summary>
    /// The notice is a warning.
    /// </summary>
    Warning,
}

/// <summary>
/// Identifies the outcome of executing a <see cref="Step"/> in a playbook.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StepOutcome
{
    /// <summary>
    /// The step succeeded.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The step failed. The <see cref="StepResult.Problem"/> property must be populated with details.
    /// </summary>
    Failed,

    /// <summary>
    /// The step was suspended. The playbook will be paused until a <see cref="ResumePlaybook"/> message is published.
    /// </summary>
    Suspended,

    /// <summary>
    /// The step succeeded and indicates the playbook run is complete, skipping any subsequent steps.
    /// </summary>
    CompletePlaybook,

    /// <summary>
    /// The step has been cancelled.
    /// </summary>
    Cancelled
}
