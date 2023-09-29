using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Azure.AI.OpenAI;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenAI_API.Chat;
using OpenAI_API.Completions;

namespace Serious.Abbot.AI;

/// <summary>
/// Following a pattern described in the paper "ReAct" (Reasoning + Acting), this is a response from an AI
/// that includes a thought and an action. The thought is a string that describes the AI's reasoning for
/// the result it provided and the action is the result.
/// </summary>
/// <remarks>
/// The paper https://arxiv.org/pdf/2210.03629.pdf.
/// </remarks>
/// <param name="Thought">Why the AI produced the result it did.</param>
/// <param name="Action">The result.</param>
public record Reasoned<T>(string Thought, T Action)
{
    public override string ToString()
    {
        return JsonConvert.SerializeObject(this, Reasoned.SerializerSettings);
    }

    /// <summary>
    /// Produces a <see cref="Reasoned{TTarget}"/> by using the provided mapper function
    /// to convert the action in this instance to <typeparamref name="TTarget"/>
    /// </summary>
    /// <param name="mapper">The function that performs the mapping</param>
    /// <typeparam name="TTarget">The target type for the new instance.</typeparam>
    public Reasoned<TTarget> Map<TTarget>(Func<T, TTarget> mapper) => new(Thought, mapper(Action));
}

public static partial class Reasoned
{
    public static readonly JsonSerializerSettings SerializerSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Formatting = Formatting.Indented,
    };

    /// <summary>
    /// Returns a list of reasoned actions from a chat result.
    /// </summary>
    /// <param name="result">The <see cref="ChatResult"/>.</param>
    public static IReadOnlyList<Reasoned<string>> FromChatResult(ChatResult result)
        => result.GetResultText() is { } resultText
            ? Parse(resultText).ToList()
            : Array.Empty<Reasoned<string>>();

    /// <summary>
    /// Returns a list of reasoned actions from a completion result.
    /// </summary>
    /// <param name="result">The <see cref="CompletionResult"/>.</param>
    public static IReadOnlyList<Reasoned<string>> FromChatResult(CompletionResult result)
        => result.GetResultText() is { } resultText
            ? Parse(resultText).ToList()
            : Array.Empty<Reasoned<string>>();

    /// <summary>
    /// Returns a list of reasoned actions from a chat result.
    /// </summary>
    /// <param name="result">The <see cref="ChatResult"/>.</param>
    public static IReadOnlyList<Reasoned<string>> FromChatResult(Completions result)
        => result.GetResultText() is { } resultText
            ? Parse(resultText).ToList()
            : Array.Empty<Reasoned<string>>();

    /// <summary>
    /// Parses the text of an AI result into a collection of <see cref="ReasonedAction"/> instances.
    /// </summary>
    /// <remarks>
    /// When asking AI to do tasks, we ask it to explain its thought and provide an action formatted as:
    /// [Thought]Customer asked for the answer to the life, universe, and everything.[/Thought]
    /// [Action]42[/Action]
    ///
    /// Thought and action should always begin on a new line.
    /// </remarks>
    /// <param name="input">The result of an AI operation.</param>
    /// <returns></returns>
    public static IEnumerable<Reasoned<string>> Parse(string input)
    {
        // When asking AI to do tasks, we ask it to explain its thought and provide an action formatted as:
        // [Thought]Customer asked for the answer to the life, universe, and everything.[/Thought]
        // [Action]42[/Action]
        // Thought and action should always begin on a new line.
        // A response can have multiple thoughts and actions.
        var matches = ReasonedActionRegex().Matches(input);
        return matches
            .Select(match => new Reasoned<string>(match.Groups["thought"].Value, match.Groups["action"].Value));
    }

    public static Reasoned<T>? ParseJson<T>(string input, JsonSerializer serializer)
    {
        input = ExtractFirstCodeFence(input);

        using var reader = new JsonTextReader(new StringReader(input));
        return serializer.Deserialize<Reasoned<T>>(reader);
    }

    internal static string ExtractFirstCodeFence(string input)
    {
        // Do some heuristic trimming to handle markdown quoting.
        // There's a risk here that the message _contains_ markdown code fences.
        // Consider if the AI is providing a code example in it's response.
        // If necessary, we can build a fancier parser by:
        // 1. Scanning for either a code fence '```' or a JSON object '{'
        // 2. If we find a code fence, expect a JSON object, parse until the end of it, then return.
        // 3. If we find a JSON object, parse until the end of it, then return.
        //    (Importantly: We don't try to find a code fence until we've parsed the JSON object fully)
        // 4. If we find neither, continue scanning forward and return to 1

        // Scan by lines for a line that starts ```
        // If we find one, scan forward until we find the next line that starts ```
        var fenceContent = new StringBuilder();
        var inFence = false;
        foreach (var line in input.Split('\n'))
        {
            if (line.Length >= 3 && line[..3] == "```")
            {
                if (inFence)
                {
                    return fenceContent.ToString();
                }

                inFence = true;
            }
            else if (inFence)
            {
                // Why not AppendLine? Because we want to preserve the original line endings.
                // We only split on '\n', so if for some reason there's a \r\n, then 'line' will end with that '\r'.
                fenceContent.Append(CultureInfo.InvariantCulture, $"{line}\n");
            }
        }

        if (inFence)
        {
            // Entered a fence but didn't exit it, just return the content we found.
            return fenceContent.ToString();
        }

        // Never entered a fence. So there never was one.
        return input;
    }

    [GeneratedRegex(@"\[Thought\](?<thought>.*?)\[/Thought\]\s*\[Action\](?<action>.*?)\[/Action\]", RegexOptions.Singleline)]
    private static partial Regex ReasonedActionRegex();
}
