using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;

namespace Serious.Abbot.Entities;

/// <summary>
/// Collects well-known system form names.
/// </summary>
public static class SystemForms
{
    /// <summary>
    /// A prefix assigned to all system forms.
    /// If/when users have the ability to create their own forms, they will be forbidden from using this prefix.
    /// </summary>
    public static readonly string SystemPrefix = "system.";

    public static readonly string CreateZendeskTicket = SystemPrefix + "create_zendesk_ticket";
    public static readonly string CreateGitHubIssue = SystemPrefix + "create_github_issue";
    public static readonly string CreateHubSpotTicket = SystemPrefix + "create_hubspot_ticket";
    public static readonly string CreateGenericTicket = SystemPrefix + "create_generic_ticket";

    /// <summary>
    /// A dictionary of form names to their default <see cref="FormDefinition"/>, which should be used if there is no custom form for the organization.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, FormDefinition> Definitions =
        new Dictionary<string, FormDefinition>()
        {
            [CreateGenericTicket] =
                new(
                    new()
                    {
                        Id = "name",
                        Title = "Subject",
                        Type = FormFieldType.SingleLineText,
                        Required = true,
                        InitialValue = "",
                    },
                    new()
                    {
                        Id = "description",
                        Title = "Description",
                        Type = FormFieldType.MultiLineText,
                        Required = false,
                        InitialValue = "{{Conversation.PlainTextTitle}}",
                    }),
            [CreateZendeskTicket] =
                new(
                    new()
                    {
                        Id = "comment",
                        Title = "Description",
                        Type = FormFieldType.MultiLineText,
                        Required = false,
                        InitialValue = "{{Conversation.Title}}",
                    },
                    new()
                    {
                        Id = "priority",
                        Title = "Priority",
                        Type = FormFieldType.DropdownList,
                        Required = true,
                        InitialValue = "normal",
                        Options =
                        {
                            new("Low", "low"),
                            new("Normal", "normal"),
                            new("High", "high"),
                            new("Urgent", "urgent"),
                        }
                    }),
            [CreateGitHubIssue] =
                new(
                    new()
                    {
                        Id = "title",
                        Title = "Title",
                        Type = FormFieldType.SingleLineText,
                        Required = true,
                        InitialValue = "",
                    },
                    new()
                    {
                        Id = "body",
                        Title = "Body",
                        Type = FormFieldType.MultiLineText,
                        Required = false,
                        InitialValue = "{{Conversation.PlainTextTitle}}",
                    },
                    new()
                    {
                        Id = "footer",
                        Type = FormFieldType.FixedValue,
                        InitialValue =
                            "_Created by [{{Actor.DisplayName}}]({{Actor.PlatformUrl}})" +
                            " from this [Slack thread]({{Conversation.MessageUrl}})." +
                            " • [View on ab.bot]({{Conversation.Url}})_",
                    }),
            [CreateHubSpotTicket] =
                new(
                    new()
                    {
                        Id = "subject",
                        Title = "Subject",
                        Type = FormFieldType.SingleLineText,
                        Required = true,
                        InitialValue = "",
                    },
                    new()
                    {
                        Id = "content",
                        Title = "Description",
                        Type = FormFieldType.MultiLineText,
                        Required = false,
                        InitialValue = "{{Conversation.PlainTextTitle}}",
                    },
                    new()
                    {
                        Id = "hs_ticket_priority",
                        Title = "Priority",
                        Type = FormFieldType.DropdownList,
                        Required = false,
                        InitialValue = "LOW",
                        Options =
                        {
                            new("Low", "LOW"),
                            new("Medium", "MEDIUM"),
                            new("High", "HIGH"),
                        }
                    }),
        };
}

/// <summary>
/// Represents a custom form that can be associated with an <see cref="Organization"/>.
/// </summary>
public class Form : OrganizationEntityBase<Form>, ITrackedEntity
{
    /// <summary>
    /// Gets or sets the unique "Key" identifying this form in the organization.
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets a boolean indicating if the form is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the definition of the form.
    /// </summary>
    [Column(TypeName = "jsonb")]
    // Why string instead of FormDefinition? Well, blame the In-Memory provider.
    // Because our tests use the In-Memory provider, we're limited to the types it supports or our tests may fail to initialize.
    // The In-Memory provider doesn't support JSON, so we use a string instead.
    public string Definition { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the <see cref="User"/> who created the form.
    /// This may be a staff user or a user of the organization.
    /// </summary>
    public User Creator { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the <see cref="User"/> who created the form.
    /// This may be a staff user or a user of the organization.
    /// </summary>
    public int CreatorId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the form was last modified.
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="User"/> who last modified the form.
    /// This may be a staff user or a user of the organization.
    /// </summary>
    public User ModifiedBy { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ID of the <see cref="User"/> who last modified the form.
    /// This may be a staff user or a user of the organization.
    /// </summary>
    public int ModifiedById { get; set; }
}

/// <summary>
/// Describes a custom form which could be used for Ticket creation.
/// </summary>
public class FormDefinition
{
    public static readonly int CurrentVersion = 1;

    public FormDefinition()
    {
    }

    public FormDefinition(params FormFieldDefinition[] fields)
    {
        Fields = fields.ToList();
    }

    /// <summary>
    /// Gets or sets the version of the form definition format in use.
    /// Used to handle migrations from incompatible versions.
    /// </summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// The fields to show in the form.
    /// </summary>
    public IList<FormFieldDefinition> Fields { get; set; } = new List<FormFieldDefinition>();
}

/// <summary>
/// Describes a single form field in a custom form.
/// </summary>
[DebuggerDisplay("{Type} {Id} {Title}")]
public class FormFieldDefinition
{
    /// <summary>
    /// Gets or sets the ID of the field.
    /// </summary>
    /// <remarks>
    /// The ID has special meaning to whatever process the form is used for.
    /// For example the ID "priority" in "system.create_zendesk_ticket" maps to the Zendesk ticket priority.
    /// </remarks>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of this field.
    /// </summary>
    public FormFieldType Type { get; init; }

    /// <summary>
    /// Gets or sets the label to display for this field.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description to display for this field.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets or sets the initial value for this field before the user has interacted with it. If this is a
    /// multiple-selection, then this is an array of strings.
    /// </summary>
    public object? InitialValue { get; init; }

    /// <summary>
    /// Gets or sets the default value or values for this field, if it is not specified by the user.
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// Gets or sets a boolean indicating whether this field is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// Gets or sets the options to display for this field if it is a <see cref="FormFieldType.DropdownList"/> or <see cref="FormFieldType.RadioList"/>.
    /// </summary>
    public IList<FormSelectOption> Options { get; init; } = new List<FormSelectOption>();
}

/// <summary>
/// Represents a potential option in a <see cref="FormFieldType.RadioList"/> or <see cref="FormFieldType.DropdownList"/> field.
/// </summary>
/// <param name="Text">The text to display for the option.</param>
/// <param name="Value">The underlying value to set when this option is selected.</param>
public record FormSelectOption(string Text, string Value);

public enum FormFieldType
{
    /// <summary>
    /// Indicates an unknown field type.
    /// This should not be set by the user.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Indicates that the form field is a fixed value that cannot be edited.
    /// The <see cref="FormFieldDefinition.InitialValue"/> will always be used for this field.
    /// No UI will be rendered for the field.
    /// </summary>
    FixedValue,

    /// <summary>
    /// Indicates that the form field should be edited as a single-line text field.
    /// The value will be a string.
    /// </summary>
    SingleLineText,

    /// <summary>
    /// Indicates that the form field should be edited as a multi-line text field.
    /// The value will be a string.
    /// </summary>
    MultiLineText,

    /// <summary>
    /// Indicates that the form field should be edited as single checkbox.
    /// The value will be the string "true" or "false", depending on if the checkbox is checked.
    /// </summary>
    Checkbox,

    /// <summary>
    /// Indicates that the form field should be edited as a radio-button list.
    /// </summary>
    RadioList,

    /// <summary>
    /// Indicates that the form field should be edited as a drop-down list.
    /// The value will be a string.
    /// </summary>
    DropdownList,

    /// <summary>
    /// Indicates that the form field should be edited as a multiple selection drop-down list.
    /// The value will be an array of strings.
    /// </summary>
    MultiDropdownList,

    /// <summary>
    /// Indicates that the form field should be edited as a date picker.
    /// </summary>
    DatePicker,

    /// <summary>
    /// Indicates that the form field should be edited as a time picker.
    /// </summary>
    TimePicker,
}
