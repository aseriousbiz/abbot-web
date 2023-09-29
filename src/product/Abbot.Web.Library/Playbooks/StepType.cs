using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks.Triggers;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Describes the type of a <see cref="Step"/>
/// </summary>
/// <param name="Name">The name of this type</param>
/// <param name="Kind">The <see cref="StepKind"/> of this step type</param>
public record StepType(string Name, StepKind Kind)
{
    /// <summary>
    /// Gets or inits a <see cref="StepPresentation"/> that describes how to present this step type in the UI.
    /// </summary>
    public StepPresentation Presentation { get; init; } = new()
    {
        Label = "",
        Description = "",
    };

    /// <summary>
    /// Gets a or inits the category of this step type.
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Gets or inits a boolean indicating if the step has "unbounded" outputs.
    /// </summary>
    /// <remarks>
    /// A step with "unbounded" outputs means there is no set schema to the outputs it can produce.
    /// For example, the <see cref="CustomerInfoSubmittedTrigger"/> has an unbounded output because the payload can be anything.
    /// </remarks>
    public bool HasUnboundedOutputs { get; init; }

    /// <summary>
    /// Gets a list of all the <see cref="StepProperty"/>s that this step type accepts as inputs.
    /// </summary>
    public IList<StepProperty> Inputs { get; init; } = new List<StepProperty>();

    /// <summary>
    /// Gets a list of all the <see cref="StepProperty"/>s that this step type produces as outputs.
    /// </summary>
    public IList<StepProperty> Outputs { get; } = new List<StepProperty>();

    /// <summary>
    /// Gets a list of all the named branches that can be associated with this step type.
    /// </summary>
    public IList<StepBranch> Branches { get; } = new List<StepBranch>();

    /// <summary>
    /// Indicates if this step type is visible to staff only.
    /// </summary>
    /// <remarks>
    /// A staff-only step type is only visible to staff users.
    /// If the step is in a playbook but the viewer is not staff, the step will be shown as an "unknown" step.
    /// A staff-only step can still be run by non-staff users if it is in a playbook.
    /// </remarks>
    public bool StaffOnly { get; init; }

    /// <summary>
    /// A list of feature flags that must be ALL be enabled for this step type to be visible.
    /// </summary>
    public IList<string> RequiredFeatureFlags { get; } = new List<string>();

    /// <summary>
    /// A list of integrations that must all be present for this step to be visible.
    /// If any of these integrations are missing, the step will be hidden and existing instances will be marked with a warning.
    /// </summary>
    public IList<IntegrationType> RequiredIntegrations { get; } = new List<IntegrationType>();

    /// <summary>
    /// If <c>true</c>, this step type is deprecated and should not be used. We will filter it out of the UI when
    /// adding steps.
    /// </summary>
    public bool Deprecated { get; init; }

    /// <summary>
    /// Specifies the dispatch types that are valid for this step type.
    /// </summary>
    public HashSet<DispatchType> AdditionalDispatchTypes { get; } = new();

    /// <summary>
    /// If populated, these are the only triggers that can be used with this step type.
    /// </summary>
    public IList<string> RequiredTriggers { get; } = new List<string>();
}

/// <summary>
/// Describes type-specific information about
/// </summary>
public class StepPresentation
{
#pragma warning disable CA1802
    static readonly string DefaultStepIcon = "fa-circle-play";
#pragma warning restore CA1802

    /// <summary>
    /// Gets or inits the Font Awesome Icon name to use for this step type.
    /// </summary>
    public string Icon { get; init; } = DefaultStepIcon;

    /// <summary>
    /// The label for the step.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// A summary of what the step does.
    /// </summary>
    public required string Description { get; init; }
}

/// <summary>
/// Describes the kind of a <see cref="StepType"/>
/// </summary>
public enum StepKind
{
    /// <summary>
    /// The <see cref="StepType"/> provides a Trigger.
    /// </summary>
    Trigger,

    /// <summary>
    /// The <see cref="StepType"/> provides an Action.
    /// </summary>
    Action
}

/// <summary>
/// Represents a branch that a step could take.
/// </summary>
/// <param name="Name">The name that will be used as the key in <see cref="ActionStep.Branches"/>, to map this branch on to an <see cref="ActionSequence"/> to run.</param>
/// <param name="Title">The title that will be displayed for this branch in the UI.</param>
public record StepBranch(string Name, string Title);

/// <summary>
/// Represents an input or output property of a <see cref="StepType"/>
/// </summary>
/// <param name="Name">The name of the property.</param>
/// <param name="Title">The user-friendly title of the property for displaying in UI.</param>
/// <param name="Type">A <see cref="PropertyType"/> describing the values accepted by this property.</param>
public record StepProperty(string Name, string Title, PropertyType Type)
{
    /// <summary>
    /// Gets or inits a boolean indicating if the property is required.
    /// If the property is required, it must have a non-<c>null</c> value in order for the step to be saved.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// A more detailed description of the property
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The context to show when choosing an expression as an input or condition for a property.
    /// </summary>
    public string? ExpressionContext { get; init; }

    /// <summary>
    /// The property's default value.
    /// </summary>
    public object? Default { get; init; }

    /// <summary>
    /// A placeholder value to show if the property is empty.
    /// </summary>
    /// <remarks>
    /// This is only used to show a "ghost" placeholder in the property editor.
    /// Only some property type editors support Placeholders.
    /// For example, the <see cref="PropertyTypeKind.String"/> editor supports placeholders, but the <see cref="PropertyTypeKind.Schedule"/> editor does not.
    /// </remarks>
    public string? Placeholder { get; init; }

    /// <summary>
    /// The property and its value should be hidden when not in staff mode.
    /// </summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// Old names for the property, newest to oldest, to be migrated to the current <see cref="Name"/>.
    /// </summary>
    public IReadOnlyList<string> OldNames { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Describes the set of values that a <see cref="StepProperty"/> can accept.
/// </summary>
/// <remarks>
/// The Playbook Type System is based on the JSON Schema Type System (since it exists and is Good Enough™️).
/// See https://json-schema.org/understanding-json-schema/reference/type.html for more information.
/// We do NOT implement the entire JSON Schema Type System, but we use it as a well-known reference point.
/// </remarks>
public record PropertyType
{
#pragma warning disable CA1720
    public static readonly PropertyType Boolean = new() { Kind = PropertyTypeKind.Boolean };
    public static readonly PropertyType Channel = new() { Kind = PropertyTypeKind.Channel };
    public static readonly PropertyType Channels = new() { Kind = PropertyTypeKind.Channel, AllowMultiple = true };
    public static readonly PropertyType Comparand = new() { Kind = PropertyTypeKind.Comparand };
    public static readonly PropertyType ComparisonType = new() { Kind = PropertyTypeKind.ComparisonType };
    public static readonly PropertyType Conversation = new() { Kind = PropertyTypeKind.Conversation };
    public static readonly PropertyType Customer = new() { Kind = PropertyTypeKind.Customer };
    public static readonly PropertyType CustomerSegments = new() { Kind = PropertyTypeKind.CustomerSegments };
    public static readonly PropertyType Duration = new() { Kind = PropertyTypeKind.Duration };
    public static readonly PropertyType Emoji = new() { Kind = PropertyTypeKind.Emoji, AllowMultiple = true };
    public static readonly PropertyType Float = new() { Kind = PropertyTypeKind.Float };
    public static readonly PropertyType Integer = new() { Kind = PropertyTypeKind.Integer };
    public static readonly PropertyType Member = new() { Kind = PropertyTypeKind.Member };
    public static readonly PropertyType Members = new() { Kind = PropertyTypeKind.Member, AllowMultiple = true };
    public static readonly PropertyType Message = new() { Kind = PropertyTypeKind.Message };
    public static readonly PropertyType MessageTarget = new() { Kind = PropertyTypeKind.MessageTarget };
    public static readonly PropertyType NotificationType = new() { Kind = PropertyTypeKind.NotificationType };
    public static readonly PropertyType Poll = new() { Kind = PropertyTypeKind.Options, Hint = UIHint.Poll };
    public static readonly PropertyType PredefinedExpression = new() { Kind = PropertyTypeKind.PredefinedExpression };
    public static readonly PropertyType RichText = new() { Kind = PropertyTypeKind.RichText };
    public static readonly PropertyType Schedule = new() { Kind = PropertyTypeKind.Schedule };
    public static readonly PropertyType SelectedOption = new() { Kind = PropertyTypeKind.SelectedOption };
    public static readonly PropertyType Signal = new() { Kind = PropertyTypeKind.Signal };
    public static readonly PropertyType Skill = new() { Kind = PropertyTypeKind.Skill };
    public static readonly PropertyType String = new() { Kind = PropertyTypeKind.String };
    public static readonly PropertyType Ticket = new() { Kind = PropertyTypeKind.Ticket };
    public static readonly PropertyType Timezone = new() { Kind = PropertyTypeKind.Timezone };
#pragma warning restore CA1720

    public static PropertyType SlackMrkdwn(int rows = 7) =>
        new() { Kind = PropertyTypeKind.RichText, Hint = UIHint.SlackMrkdwn, EditRows = rows };

    /// <summary>
    /// The <see cref="PropertyTypeKind"/> kind of this property.
    /// </summary>
    public required PropertyTypeKind Kind { get; init; }

    /// <summary>
    /// Indicates if this property can accept multiple values.
    /// </summary>
    public bool AllowMultiple { get; init; }

    /// <summary>
    /// A <see cref="Playbooks.UIHint"/> suggesting how to render this property, if applicable.
    /// </summary>
    public UIHint? Hint { get; init; }

    /// <summary>
    /// How many rows should a rich editor show?
    /// Ignored if <see cref="UIHint"/> is not <see cref="UIHint.HTML"/> or <see cref="UIHint.SlackMrkdwn"/>.
    /// </summary>
    public int? EditRows { get; init; }

    // TODO: We can add more properties here to describe more complicated types (arrays, dictionaries, etc.) AS NEEDED.
}

/// <summary>
/// The kind of a <see cref="PropertyType"/>.
/// </summary>
#pragma warning disable CA1720
public enum PropertyTypeKind
{
    /// <summary>
    /// Unicode text.
    /// </summary>
    String,

    /// <summary>
    /// An integer of no less than 64 bits.
    /// </summary>
    Integer,

    /// <summary>
    /// A boolean, either true or false.
    /// </summary>
    Boolean,

    /// <summary>
    /// A time duration, represented as a string in ISO 8601 format.
    /// <seealso href="https://en.wikipedia.org/wiki/ISO_8601#Durations"/>
    /// </summary>
    Duration,

    /// <summary>
    /// A floating-point number of at least 64-bit precision.
    /// </summary>
    Float,

    /// <summary>
    /// A channel.
    /// </summary>
    Channel,

    /// <summary>
    /// A Slack message target, e.g. a channel or message.
    /// </summary>
    MessageTarget,

    /// <summary>
    /// A Slack message.g. a channel or message.
    /// </summary>
    Message,

    /// <summary>
    /// An Abbot <see cref="Entities.Conversation"/>.
    /// </summary>
    Conversation,

    /// <summary>
    /// A comparison type.
    /// </summary>
    ComparisonType,

    /// <summary>
    /// A property that accepts a pre-defined handlebars expression chosen from a drop down.
    /// </summary>
    PredefinedExpression,

    /// <summary>
    /// The name of a signal.
    /// </summary>
    Signal,

    /// <summary>
    /// The name of a skill.
    /// </summary>
    Skill,

    /// <summary>
    /// A customer.
    /// </summary>
    Customer,

    /// <summary>
    /// A schedule.
    /// </summary>
    Schedule,

    /// <summary>
    /// An IANA timezone.
    /// </summary>
    Timezone,

    /// <summary>
    /// A selected option.
    /// </summary>
    SelectedOption,

    /// <summary>
    /// Either a warning or a deadline.
    /// </summary>
    NotificationType,

    /// <summary>
    /// A poll preset (or custom options, someday).
    /// </summary>
    Options,

    /// <summary>
    /// Text in a rich text (TipTap JSONContent) format.
    /// </summary>
    RichText,

    /// <summary>
    /// A value to compare against. The editor for this property depends on the comparison type used, etc.
    /// </summary>
    Comparand,

    /// <summary>
    /// A <see cref="Entities.Member"/>.
    /// </summary>
    Member,

    /// <summary>
    /// An emoji
    /// </summary>
    Emoji,

    /// <summary>
    /// A ticket
    /// </summary>
    Ticket,

    /// <summary>
    /// Customer segments
    /// </summary>
    CustomerSegments,
}
#pragma warning restore CA1720


public enum UIHint
{
    HTML,
    SlackMrkdwn,
    Poll,
}
