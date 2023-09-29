using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Provides services for serializing, deserializing, and validating <see cref="PlaybookDefinition"/>s
/// </summary>
public static partial class PlaybookFormat
{
    public static readonly Regex ValidIdentifierRegex = CreateIdentifierRegex();
    public static readonly Regex ValidSequenceNameRegex = CreateSequenceNameRegex();
    public static readonly Regex ValidStepTypeNameRegex = CreateStepTypeNamePattern();

    static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        DefaultValueHandling = DefaultValueHandling.Ignore,
        Converters =
        {
            new StringEnumConverter(),
        }
    };

    /// <summary>
    /// Serializes a <see cref="PlaybookDefinition"/> to a JSON string.
    /// </summary>
    /// <param name="playbook">The <see cref="PlaybookDefinition"/>.</param>
    /// <returns>The serialized JSON string.</returns>
    public static string Serialize(PlaybookDefinition playbook, bool indented = true) =>
        JsonConvert.SerializeObject(playbook, indented ? Formatting.Indented : default, SerializerSettings);

    /// <summary>
    /// Deserializes a <see cref="PlaybookDefinition"/> from a JSON string.
    /// </summary>
    /// <param name="value">The serialized JSON string.</param>
    /// <returns>The deserialized <see cref="PlaybookDefinition"/></returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static PlaybookDefinition? Deserialize(string? value) => value is null ? null :
        JsonConvert.DeserializeObject<PlaybookDefinition>(value, SerializerSettings).Require();

    /// <summary>
    /// Validates the provided <see cref="PlaybookDefinition"/>.
    /// </summary>
    /// <param name="playbook">The playbook definition to validate.</param>
    /// <returns>A list of validation errors, if any.</returns>
    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
    public static IReadOnlyList<PlaybookDiagnostic> Validate(PlaybookDefinition playbook)
    {
        var diagnostics = new List<PlaybookDiagnostic>();

        // Check some things that might happen if the JSON is malformed
        if (playbook.FormatVersion != PlaybookDefinition.CurrentVersion || playbook.Sequences is null || playbook.StartSequence is null || playbook.Triggers is null)
        {
            diagnostics.Add(new($"The playbook is invalid"));
            return diagnostics;
        }

        // Check that the start sequence exists
        if (!playbook.Sequences.ContainsKey(playbook.StartSequence))
        {
            diagnostics.Add(new($"The start sequence '{playbook.StartSequence}' does not exist."));
        }

        // Check that all IDs are unique
        var ids = new HashSet<string>();
        var allNodes = playbook.Triggers
            .Concat<Step>(playbook.Sequences.Values.SelectMany(s => s.Actions));

        foreach (var (id, _) in playbook.Sequences)
        {
            if (!ValidSequenceNameRegex.IsMatch(id))
            {
                diagnostics.Add(new($"The Sequence ID '{id}' is not valid."));
            }
        }

        foreach (var node in allNodes)
        {
            // Check some things that might happen if the JSON is malformed
            if (node.Id is null || node.Type is null)
            {
                diagnostics.Clear();
                diagnostics.Add(new($"The playbook is invalid"));
                return diagnostics;
            }

            // Validate ID
            if (!ValidIdentifierRegex.IsMatch(node.Id))
            {
                diagnostics.Add(new($"The Step ID '{node.Id}' is not valid."));
            }

            if (!ids.Add(node.Id))
            {
                diagnostics.Add(new($"The Step ID '{node.Id}' is not unique."));
            }

            if (!ValidStepTypeNameRegex.IsMatch(node.Type))
            {
                diagnostics.Add(new($"Step '{node.Id}' references invalid Step Type Name '{node.Type}'"));
            }

            // Validate inputs
            foreach (var (key, val) in node.Inputs)
            {
                if (!ValidIdentifierRegex.IsMatch(key))
                {
                    diagnostics.Add(new($"Input name '{key}' on step '{node.Id}' is not valid"));
                }
            }

            // Validate branches
            if (node is ActionStep actionStep)
            {
                foreach (var (key, val) in actionStep.Branches)
                {
                    if (!ValidIdentifierRegex.IsMatch(key))
                    {
                        diagnostics.Add(new($"Branch name '{key}' on step '{node.Id}' is not valid"));
                    }

                    if (!ValidSequenceNameRegex.IsMatch(val))
                    {
                        diagnostics.Add(
                            new($"Branch '{key}' on step '{node.Id}' references invalid sequence name '{val}'"));
                    }
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Validates the provided <see cref="StepType"/>.
    /// </summary>
    /// <param name="stepType">The step type to validate.</param>
    /// <returns>A list of validation errors, if any.</returns>
    public static IReadOnlyList<PlaybookDiagnostic> Validate(StepType stepType)
    {
        var diagnostics = new List<PlaybookDiagnostic>();
        if (!ValidStepTypeNameRegex.IsMatch(stepType.Name))
        {
            diagnostics.Add(new($"Step type name '{stepType.Name}' is invalid"));
        }

        foreach (var prop in stepType.Inputs.Concat(stepType.Outputs))
        {
            if (!PlaybookFormat.ValidIdentifierRegex.IsMatch(prop.Name))
            {
                diagnostics.Add(new($"Property name '{prop.Name}' in step type '{stepType.Name}' is invalid"));
            }
        }

        return diagnostics;
    }

    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex CreateIdentifierRegex();

    // We allow ':' in sequence names because it allows for a useful separator between Step ID and Branch Name, both of which must be identifiers.
    // The ':' character is not allowed in an identifier which means we can construct Sequence Names so that they can be split into their constituent parts, IF we want to.
    [GeneratedRegex("^[a-zA-Z_][:a-zA-Z0-9_]*$")]
    private static partial Regex CreateSequenceNameRegex();

    // The regex pattern for a step type name.
    // A step type name MUST start with a lowercase ASCII letter (a-z)
    // The rest of the name MUST consist of lowercase ASCII letters (a-z), digits (0-9), hyphens (-), periods (.), and colons (:).
    // Notably: Underscores and uppercase ASCII are NOT PERMITTED.
    // We can relax these rules if we need to.
    //
    // Essentially:
    // * Use lower-case words
    // * Separate words with '-'
    // * Separate categories/namespaces with '.' and ':' (e.g. 'system.webhook' or 'integration:zendesk.post-message')
    [GeneratedRegex("""^[a-z]([a-z0-9\.:-]*)$""")]
    private static partial Regex CreateStepTypeNamePattern();
}

/// <summary>
/// Represents a diagnostic (error/warning/etc.) for a playbook definition.
/// Currently, all diagnostics are errors.
/// </summary>
/// <param name="Message">The diagnostic message</param>
// TODO: We can add position information (relevant IDs, etc.) as needed.
public record PlaybookDiagnostic(string Message);
