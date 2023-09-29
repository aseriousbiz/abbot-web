using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Playbooks.Outputs;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Playbooks;

/// <summary>
/// Describes the type of a trigger <see cref="Step"/>.
/// </summary>
/// <remarks>
/// Not every trigger needs to implement this interface. Only the ones that require additional conditions to be met
/// before they can be triggered.
/// </remarks>
public interface ITriggerType
{
    /// <summary>
    /// Gets the <see cref="StepType"/> that describes this action.
    /// </summary>
    StepType Type { get; }

    /// <summary>
    /// Returns <c>true</c> if this trigger should be triggered based on the given <paramref name="triggerStep"/> and
    /// <paramref name="outputs"/>.
    /// </summary>
    /// <param name="triggerStep">The trigger step.</param>
    /// <param name="outputs">The outputs for this trigger.</param>
    /// <param name="reason">Why return value is what it is.</param>
    /// <returns><c>true</c> if this trigger should be triggered.</returns>
    bool ShouldTrigger(TriggerStep triggerStep, IDictionary<string, object?> outputs, out string reason)
    {
        reason = "No conditions.";
        return true;
    }
}

public static class TriggerStepExtensions
{
    static AbbotJsonFormat JsonFormat => StepContext.JsonFormat;

    /// <summary>
    /// Returns true if the input value for the specified key matches the output value for the same key.
    /// </summary>
    /// <param name="triggerStep">The trigger.</param>
    /// <param name="key">The name of the input/output.</param>
    /// <param name="outputs">The current set of outputs.</param>
    /// <param name="reason">Why return value is what it is.</param>
    /// <param name="stringComparison">The type of comparison.</param>
    /// <returns></returns>
    public static bool InputValueForKeyMatchesOutputValue(
        this TriggerStep triggerStep,
        string key,
        IDictionary<string, object?> outputs,
        out string reason,
        StringComparison stringComparison = StringComparison.Ordinal)
    {
        if (!triggerStep.Inputs.TryGetValue(key, out var inputValueObject))
        {
            reason = $"Expected input '{key}' not found.";
            return false;
        }
        if (!outputs.TryGetValue(key, out var outputValueObject))
        {
            reason = $"Expected output '{key}' not found.";
            return false;
        }
        if (inputValueObject is not string inputValue)
        {
            reason = $"Expected input '{key}' to be a string; received {inputValueObject?.GetType().Name ?? "null"}.";
            return false;
        }
        if (outputValueObject is not string outputValue)
        {
            reason = $"Expected output '{key}' to be a string; received {outputValueObject?.GetType().Name ?? "null"}.";
            return false;
        }
        if (!string.Equals(inputValue, outputValue, stringComparison))
        {
            reason = $"Output '{key}' value not equal to '{inputValue}'.";
            return false;
        }
        reason = $"Output '{key}' value equal to '{inputValue}'.";
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if the trigger does not filter by customer segments or if incoming customer segments
    /// match any of the segments specified in the trigger.
    /// </summary>
    /// <param name="triggerStep">The trigger.</param>
    /// <param name="outputs">The current set of outputs.</param>
    /// <param name="reason">Why return value is what it is.</param>
    public static bool CustomerSegmentsMatchTriggerFilter(
        this TriggerStep triggerStep,
        IDictionary<string, object?> outputs,
        out string reason) =>
        triggerStep.InputValuesForKeyMatchOutputValue<Option, CustomerOutput>(
            "segments",
            option => option.Value,
            inputOptional: true,
            outputs,
            "customer",
            customer => customer.Segments,
            out reason,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <see langword="true"/> if the input values at <paramref name="inputKey"/>
    /// match at least one value from <paramref name="outputs"/> at <paramref name="outputKey"/>
    /// of type <typeparamref name="TOutput"/>.
    /// </summary>
    /// <typeparam name="TOutput">The expected output type, e.g. <see cref="CustomerOutput"/>.</typeparam>
    /// <param name="triggerStep">The <see cref="TriggerStep"/>.</param>
    /// <param name="inputKey">The expected key in <see cref="Step.Inputs"/>.</param>
    /// <param name="inputValueSelector">The string value to match from the input.</param>
    /// <param name="inputOptional">If an empty/missing input should be considered a match, e.g. no Segments selects All Customers.</param>
    /// <param name="outputs">The outputs of the currently executing trigger.</param>
    /// <param name="outputKey">The expected key in <paramref name="outputs"/>.</param>
    /// <param name="outputValuesSelector">The values to match from an output of type <typeparamref name="TOutput"/>.</param>
    /// <param name="reason">Why return value is what it is.</param>
    /// <param name="stringComparison">The <see cref="StringComparison"/> to use for comparing values.</param>
    public static bool InputValuesForKeyMatchOutputValue<TInput, TOutput>(
        this TriggerStep triggerStep,
        string inputKey,
        Func<TInput, string> inputValueSelector,
        bool inputOptional,
        IDictionary<string, object?> outputs,
        string outputKey,
        Func<TOutput, IReadOnlyCollection<string>> outputValuesSelector,
        out string reason,
        StringComparison stringComparison = StringComparison.Ordinal)
        where TInput : notnull
        where TOutput : notnull
    {
        if (!triggerStep.Inputs.TryGetValue(inputKey, out var inputValueObject))
        {
            reason = $"Input '{inputKey}' not found.";
            return inputOptional;
        }
        if (JsonFormat.Convert<TInput[]>(inputValueObject) is not { } inputs)
        {
            if (JsonFormat.Convert<TInput>(inputValueObject) is not { } input)
            {
                reason = $"Expected input '{inputKey}' to be an {typeof(TInput).Name}[]; received {inputValueObject?.GetType().Name ?? "null"}.";
                return inputOptional;
            }

            inputs = new[] { input };
        }
        if (inputs is [])
        {
            reason = $"Input '{inputKey}' is empty.";
            return inputOptional;
        }

        if (!outputs.TryGetValue(outputKey, out var outputValueObject))
        {
            reason = $"Output '{outputKey}' not found.";
            return false;
        }
        if (JsonFormat.Convert<TOutput>(outputValueObject) is not { } output)
        {
            reason = $"Expected output '{outputKey}' to be a {typeof(TOutput).Name}; received {outputValueObject?.GetType().Name ?? "null"}.";
            return false;
        }
        if (outputValuesSelector(output) is not { Count: > 0 } outputValues)
        {
            reason = $"Output '{outputKey}' has no values to match.";
            return false;
        }

        var stringComparer = StringComparer.FromComparison(stringComparison);
        foreach (var input in inputs)
        {
            var inputValue = inputValueSelector(input);
            if (outputValues.Contains(inputValue, stringComparer))
            {
                reason = $"Output '{outputKey}' matches input value '{inputValue}'.";
                return true;
            }
        }

        reason = $"Output '{outputKey}' has no matching values.";
        return false;
    }
}
