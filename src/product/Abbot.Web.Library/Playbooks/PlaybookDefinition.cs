using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using Humanizer;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Playbooks;

// IMPORTANT: Read this before digging too deep into this.
// This file lays out the SERIALIZABLE definition of a playbook.
// We should be able to easily serialize these classes to JSON and back.
// That means a few things:
// * The objects here MUST NOT have any reference cycles
// * Objects should reference each other by IDs UNLESS there is a strict single-parent/child relationship
// * Use only JSON-serializable types in properties.

/// <summary>
/// Represents the serializable definition of a playbook.
/// </summary>
/// <remarks>
/// <para>
/// The definition is versioned, because it is stored as JSON in the database.
/// In the event a breaking change needs to be made to the JSON format
/// and we cannot adjust existing customer playbooks, we will increment this number.
/// The initial version is 0, which is also the version that should be assumed if the 'formatVersion' field is not present in the serialized JSON
/// </para>
/// </remarks>
public record PlaybookDefinition : JsonSettings
{
#pragma warning disable CA1802
#pragma warning disable CA1805
    public static readonly int CurrentVersion = 0;
#pragma warning restore CA1805
#pragma warning restore CA1802

    /// <summary>
    /// The version of the playbook definition format used by this object.
    /// If this value does not match an expected version, you cannot assume the structure of the playbook definition.
    /// </summary>
    public int FormatVersion { get; init; } = CurrentVersion;

    /// <summary>
    /// A list of all the <see cref="TriggerStep"/>s representing the entry points to this playbook.
    /// </summary>
    public IList<TriggerStep> Triggers { get; } = new List<TriggerStep>();

    /// <summary>
    /// Gets or inits settings related to how the playbook should be dispatched, once triggered.
    /// </summary>
    public DispatchSettings Dispatch { get; init; } = DispatchSettings.Default;

    /// <summary>
    /// Gets or inits the ID of the <see cref="ActionSequence"/> that should be executed first.
    /// </summary>
    public required string StartSequence { get; init; }

    /// <summary>
    /// A dictionary of all the <see cref="ActionSequence"/>s in this Playbook, keyed by their ID.
    /// </summary>
    public IDictionary<string, ActionSequence> Sequences { get; } = new Dictionary<string, ActionSequence>();

    public IEnumerable<Step> EnumerateAllSteps()
    {
        foreach (var trigger in Triggers)
        {
            yield return trigger;
        }

        foreach (var sequence in Sequences.Values)
        {
            foreach (var action in sequence.Actions)
            {
                yield return action;
            }
        }
    }
}

/// <summary>
/// Describes how a playbook should be dispatched, when triggered.
/// </summary>
public record DispatchSettings
{
    public static DispatchSettings Default => new()
    {
        Type = DispatchType.Once,
    };

    /// <summary>
    /// Gets or inits a value indicating how the playbook will be dispatched.
    /// </summary>
    public required DispatchType Type { get; init; } = DispatchType.Once;

    /// <summary>
    /// The set of customer segments to filter on if <see cref="Type"/> is <see cref="DispatchType.ByCustomer"/>.
    /// </summary>
    public IList<string> CustomerSegments { get; init; } = new List<string>();

    public override string ToString() =>
        Type switch
        {
            DispatchType.ByCustomer when CustomerSegments.Count > 0 =>
                $"{Type.Humanize()} in {CustomerSegments.Humanize("or")}",
            _ => Type.Humanize(),
        };

    public virtual bool Equals(DispatchSettings? other) =>
        Type == other?.Type
        && new HashSet<string>(CustomerSegments).SetEquals(other.CustomerSegments);

    public override int GetHashCode() =>
        CustomerSegments.Distinct().Order().Aggregate(Type.GetHashCode(), HashCode.Combine);
}

/// <summary>
/// Determines how the playbook will be dispatched when triggered.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DispatchType
{
    /// <summary>
    /// A single run of the playbook will be dispatched, when triggered.
    /// </summary>
    Once,

    /// <summary>
    /// A separate run of the playbook will be dispatched for each customer in the organization, when triggered.
    /// </summary>
    [Display(Name = "Each Customer")]
    ByCustomer,
}

/// <summary>
/// Represents a single linear sequence through a playbook.
/// </summary>
/// <remarks>
/// Execution continues uninterrupted through a sequence until either:
/// * The sequence is completed
/// * An action causes execution to jump to a different sequence
/// </remarks>
public record ActionSequence
{
    /// <summary>
    /// A list of all the <see cref="Action"/>s representing the actions in this sequence.
    /// </summary>
    public IList<ActionStep> Actions { get; } = new List<ActionStep>();
}

/// <summary>
/// Represents a single step (trigger or action) in a playbook.
/// </summary>
/// <param name="Id">The unique ID of this step.</param>
/// <param name="Type">The type of this step.</param>
/// <remarks>
/// The <see cref="Type"/> of a step defines what behavior it has.
/// For example, 'slack.post-message' might be an Action that posts a message to a Slack channel.
/// Or, 'slack.channel-created' might be a Trigger that fires when a new Slack channel is created.
/// For simplicity, type names should be unique across BOTH Triggers and Actions.
/// </remarks>
public abstract record Step(string Id, string Type)
{
    /// <summary>
    /// Input bindings for this step.
    /// </summary>
    /// <remarks>
    /// Each entry in this dictionary represents a single input binding.
    /// The key is the name of the input property, as defined by the <see cref="StepType.Inputs"/> collection in the type matching the <see cref="Type"/> for this step.
    /// The value is a JSON primitive token (string, number, or boolean) representing the value to bind to that input.
    /// Numbers and booleans will be passed as-is, but strings will run through a templating language to allow for dynamic values.
    /// After templating, strings will be coerced into to the type expected by the property, to allow for templated numbers/booleans/etc.
    /// Objects and Arrays are NOT supported at this time.
    /// </remarks>
    public IDictionary<string, object?> Inputs { get; } = new Dictionary<string, object?>();
}

/// <summary>
/// Represents a single action in a playbook.
/// </summary>
public record ActionStep(string Id, string Type) : Step(Id, Type)
{
    /// <summary>
    /// Branch bindings for this step.
    /// </summary>
    /// <remarks>
    /// Each entry in this dictionary represents a single branch binding.
    /// The key is the name of the branch, as defined by the <see cref="StepBranch.Name"/> property of a branch in the <see cref="StepType.Branches"/> collection for the type matching the <see cref="Type"/> for this step.
    /// The value is the name of an <see cref="ActionSequence"/> in this playbook.
    /// If the step decides to take a given branch, it will return the name of the <see cref="StepBranch"/> it wishes to take.
    /// The step runner will then use this mapping table to determine which <see cref="ActionSequence" /> should be run next.
    /// </remarks>
    public IDictionary<string, string> Branches { get; } = new Dictionary<string, string>();
}

/// <summary>
/// Represents a single trigger in a playbook.
/// </summary>
public record TriggerStep(string Id, string Type) : Step(Id, Type);
