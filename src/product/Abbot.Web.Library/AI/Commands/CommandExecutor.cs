using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Serious.Abbot.AI.Responder;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.AI.Commands;

/// <summary>
/// Executes commands in the context of a <see cref="ResponderIteration"/>
/// </summary>
public class CommandExecutor
{
    readonly IMemoryRepository _memoryRepository;

    public CommandExecutor(IMemoryRepository memoryRepository)
    {
        _memoryRepository = memoryRepository;
    }

    /// <summary>
    /// Executes a command, returning the <see cref="IterationResult"/> from the command.
    /// </summary>
    public async Task<IterationResult> ExecuteCommandAsync(ResponderSession session, Command command)
    {
        // TODO: Fan this out into some ICommandHandlers that are registered with DI
        switch (command)
        {
            case ChatPostCommand chatPost:
                return new EndTurnResult(chatPost.Body, chatPost.Synthesized);
            case RemSearchCommand remSearch:
                return await SearchRemAsync(session, remSearch.Terms);
            case RemGetCommand remGet:
                return await GetRemAsync(session, remGet.Key);
            case NoopCommand:
                // Nothing to say, nothing to do.
                return new EndTurnResult(null, false);
            default:
                throw new InvalidOperationException($"Unrecognized command: {command.Name}");
        }
    }

    async Task<IterationResult> GetRemAsync(ResponderSession session, string key)
    {
        // Get the value from Rem
        var memory = await _memoryRepository.GetAsync(key, session.Organization);
        if (memory is null)
        {
            return new ContinueTurnResult($"I don't know what `{key}` is.");
        }

        return new ContinueTurnResult($"`{key}` is `{memory.Content}`.");
    }

    async Task<IterationResult> SearchRemAsync(ResponderSession session, IReadOnlyList<string> terms)
    {
        var memories = await _memoryRepository.SearchAsync(terms, session.Organization);
        if (memories.Count == 0)
        {
            return new ContinueTurnResult("There are NO memories matching ANY of the provided terms.");
        }

        var results = new StringBuilder(
            """
            I found the following memories:

            """);
        foreach (var memory in memories)
        {
            results.Append(CultureInfo.InvariantCulture,
                $"""

                {memory.Name} = {memory.Content}
                """);
        }

        return new ContinueTurnResult(results.ToString());
    }
}
