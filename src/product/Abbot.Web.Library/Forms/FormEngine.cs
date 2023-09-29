using System.Collections.Generic;
using System.Linq;
using HandlebarsDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Forms;

public interface IFormEngine
{
    /// <summary>
    /// Translates a <see cref="FormDefinition"/> into a set of <see cref="ILayoutBlock"/>s
    /// that can be presented in a Slack view to render the form.
    /// </summary>
    /// <param name="definition">The <see cref="FormDefinition"/> to translate.</param>
    /// <param name="templateContext">The context object to use for Handlebars templates in the form.</param>
    /// <returns>A list of <see cref="ILayoutBlock"/>s representing the form to display in Slack.</returns>
    IList<ILayoutBlock> TranslateForm(FormDefinition definition, object templateContext);

    /// <summary>
    /// Process the result of a view submission that was made on a view generated by <see cref="TranslateForm"/>.
    /// </summary>
    /// <param name="payload">The <see cref="IViewSubmissionPayload"/> received from Slack.</param>
    /// <param name="definition">The <see cref="FormDefinition"/> that was used to generate the view.</param>
    /// <param name="templateContext">The context object to use for Handlebars templates in fixed-value fields on the form.</param>
    /// <returns>A dictionary mapping field IDs to their final value as identified in the payload.</returns>
    Dictionary<string, object?> ProcessFormSubmission(
        IViewSubmissionPayload payload,
        FormDefinition definition,
        object templateContext);
}

public class FormEngine : IFormEngine
{
    static readonly JsonSerializerSettings SerializerSettings = new()
    {
        Converters =
        {
            new StringEnumConverter()
        },
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    public IList<ILayoutBlock> TranslateForm(FormDefinition definition, object templateContext)
    {
        var blocks = new List<ILayoutBlock>();
        foreach (var field in definition.Fields)
        {
            if (TranslateField(field, templateContext) is { } block)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    public Dictionary<string, object?> ProcessFormSubmission(
        IViewSubmissionPayload payload,
        FormDefinition definition,
        object templateContext)
    {
        var state = payload.View.State.Require();
        var results = new Dictionary<string, object?>();

        foreach (var field in definition.Fields)
        {
            if (field.Type is FormFieldType.FixedValue)
            {
                // Fixed-value fields weren't present in the view, so now we compute their value.
                results[field.Id] = ExecuteTemplate(field, nameof(field.InitialValue), field.InitialValue, templateContext);
            }
            else if (state.TryGetAs<IPayloadElement>("__field:" + field.Id, null, out var element))
            {
                if (element is IValueElement valueElement)
                {
                    results[field.Id] = valueElement.Value ?? ExecuteTemplate(field, nameof(field.DefaultValue), field.DefaultValue, templateContext);
                }
                else if (element is IMultiValueElement multiValueElement)
                {
                    // For multiple values we grab an array of string values.
                    results[field.Id] = multiValueElement.Values.Count switch
                    {
                        0 => ExecuteTemplate(field, nameof(field.DefaultValue), field.DefaultValue, templateContext),
                        1 => multiValueElement.Values[0],
                        _ => multiValueElement.Values.ToArray(),
                    };
                }
            }
            else
            {
                results[field.Id] = ExecuteTemplate(field, nameof(field.DefaultValue), field.DefaultValue, templateContext);
            }
        }

        return results;
    }

    static ILayoutBlock? TranslateField(FormFieldDefinition field, object templateContext)
    {
        var initialValue = ExecuteTemplate(field, nameof(field.InitialValue), field.InitialValue, templateContext);
        var id = $"__field:{field.Id}";

        // Hacky special case for the checkbox input
        if (field.Type is FormFieldType.Checkbox)
        {
            return new Actions(id, TranslateCheckbox(field, initialValue?.ToString()));
        }

        var element = field.Type switch
        {
            FormFieldType.SingleLineText => new PlainTextInput
            {
                InitialValue = initialValue?.ToString(),
                Placeholder = field.Description is null
                    ? null
                    : new PlainText(field.Description),
            },
            FormFieldType.MultiLineText => new PlainTextInput
            {
                InitialValue = initialValue?.ToString(),
                Placeholder = field.Description is null
                    ? null
                    : new PlainText(field.Description),
                Multiline = true,
            },
            FormFieldType.DropdownList => TranslateDropdown(field, initialValue),
            FormFieldType.MultiDropdownList => TranslateMultiDropDown(field, initialValue),
            FormFieldType.RadioList => TranslateRadioList(field, initialValue),
            FormFieldType.DatePicker => new DatePicker
            {
                InitialDate = initialValue?.ToString(),
                Placeholder = field.Description is null
                    ? null
                    : new PlainText(field.Description),
            },
            FormFieldType.TimePicker => new TimePicker
            {
                InitialTime = initialValue?.ToString(),
                Placeholder = field.Description is null
                    ? null
                    : new PlainText(field.Description),
            },
            FormFieldType.FixedValue => null, // Don't render anything, we'll handle this in ProcessFormSubmission
            _ => throw new NotImplementedException($"Translation for {field.Type} not yet implemented.")
        };

        if (element is null)
        {
            return null;
        }

        return new Input(field.Title, element, id)
        {
            Optional = !field.Required
        };
    }

    static IActionElement TranslateCheckbox(FormFieldDefinition field, string? initialValue)
    {
        var trueOption = new CheckOption(field.Title, "true");
        return string.Equals(initialValue, "true", StringComparison.OrdinalIgnoreCase)
            ? new CheckboxGroup(trueOption)
            {
                InitialOptions = new List<CheckOption>
                {
                    trueOption
                }
            }
            : new CheckboxGroup(trueOption);
    }

    static readonly IHandlebars HandlebarsCompiler = Handlebars.Create(
        configuration: new HandlebarsConfiguration().Configure(cfg => cfg.NoEscape = true));

    // We could do something funky with CallerArgumentException but this works and is easy enough to do.
    static object? ExecuteTemplate(FormFieldDefinition field, string templateParameterName, object? template, object templateContext)
    {
        try
        {
            if (template is JArray array)
            {
                template = array.ToObject<IReadOnlyList<string>>();
            };

            return template switch
            {
                null => null,
                IEnumerable<string> values => values
                    .Select(v => ExecuteTemplate(field, templateParameterName, v, templateContext) ?? v)
                    .Select(o => o.ToString()).ToArray(),
                _ => HandlebarsCompiler.Compile((string)template)(templateContext),
            };
        }
        catch (HandlebarsParserException hpex)
        {
            throw new InvalidFormFieldException(field.Id, templateParameterName, hpex.Message, hpex);
        }
    }

    static IInputElement TranslateRadioList(FormFieldDefinition field, object? initialValue)
    {
        var options = field.Options.Select(o => new CheckOption(new PlainText(o.Text), o.Value)).ToList();
        var initialOption = initialValue is string value
            ? options.FirstOrDefault(o => o.Value == value)
            : null;

        return new RadioButtonGroup(options)
        {
            InitialOption = initialOption
        };
    }

    static IInputElement TranslateDropdown(FormFieldDefinition field, object? initialValue)
    {
        if (field.Options.Any(f => f is not { Text.Length: > 0, Value.Length: > 0 }))
        {
            // Invalid option
            throw new InvalidFormFieldException(field.Id, nameof(field.Options), "All options must have a non-empty text and value.");
        }

        var options = field.Options.Select(o => new Option(new PlainText(o.Text), o.Value)).ToList();
        var initialOption = initialValue is string value
            ? options.FirstOrDefault(o => o.Value == value)
            : null;

        return new StaticSelectMenu(options)
        {
            InitialOption = initialOption
        };
    }

    static IInputElement TranslateMultiDropDown(FormFieldDefinition field, object? initialValue)
    {
        if (field.Options.Any(f => f is not { Text.Length: > 0, Value.Length: > 0 }))
        {
            // Invalid option
            throw new InvalidFormFieldException(field.Id, nameof(field.Options), "All options must have a non-empty text and value.");
        }

        if (field.DefaultValue is not null or IEnumerable<string>)
        {
            throw new InvalidFormFieldException(field.Id, nameof(field.DefaultValue), $"{FormFieldType.MultiDropdownList} {nameof(FormFieldDefinition.DefaultValue)} must be an array of strings.");
        }

        var options = field.Options.Select(o => new Option(new PlainText(o.Text), o.Value)).ToArray();
        var initialOptions = initialValue switch
        {
            null => null,
            IEnumerable<string> values => options.Where(o => values.Contains(o.Value)),
            // If someone passes a string (or something else), there's a good chance they're doing it wrong. For example,
            // {{Conversation.Members}} will return a string, not an array. If they want to select a single value, they
            // can pass an array of a single value.
            _ => throw new InvalidFormFieldException(field.Id, nameof(field.InitialValue), $"{FormFieldType.MultiDropdownList} {nameof(FormFieldDefinition.InitialValue)} must be an array of strings."),
        };

        return new MultiStaticSelectMenu(options)
        {
            InitialOptions = initialOptions?.ToList()
        };
    }

    public static string SerializeFormDefinition(FormDefinition definition, bool indented = false)
    {
        return JsonConvert.SerializeObject(definition,
            indented
                ? Formatting.Indented
                : Formatting.None,
            SerializerSettings);
    }

    public static FormDefinition DeserializeFormDefinition(string serializedDefinition) =>
        JsonConvert.DeserializeObject<FormDefinition>(serializedDefinition, SerializerSettings).Require();
}