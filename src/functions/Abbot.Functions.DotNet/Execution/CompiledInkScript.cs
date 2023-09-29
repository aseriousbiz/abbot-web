using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ink.Runtime;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Functions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Scripting;
using Serious.Logging;

namespace Serious.Abbot.Execution;

public class CompiledInkScript : ICompiledSkill
{
    // Ink isn't thread-safe, so we can't run multiple Ink skills at once 😭.
    static readonly SemaphoreSlim RunLock = new(1, 1);

    const string StateKey = "__ink_state";
    const string GenerationKey = "__ink_generation";
    const string ActiveMessageIdKey = "__ink_active_message_id";
    readonly Story _story;

    static readonly ILogger<CompiledInkScript> Log = ApplicationLoggerFactory.CreateLogger<CompiledInkScript>();

    IExtendedBot Bot { get; set; } = null!;

    IExtendedBrain Brain => Bot.ExtendedBrain;

    public CompiledInkScript(string name, string json)
    {
        Name = name;
        _story = new Story(json);
        _story.BindExternalFunction("signal",
            (string signal, string arguments) => {
                if (!Bot.SignalAsync(signal, arguments).RunSync())
                {
                    throw new TimeoutException($"Timeout sending signal {signal}");
                }
            },
            false);
    }

    public async Task<Exception?> RunAsync(IExtendedBot skillContext)
    {
        await Log.LogElapsedAsync("AcquireRunLock", () => RunLock.WaitAsync());
        try
        {
            return await RunLockedAsync(skillContext);
        }
        finally
        {
            RunLock.Release();
        }
    }

    async Task<Exception?> RunLockedAsync(IExtendedBot skillContext)
    {
        Bot = skillContext;

        // Check if we're resetting
        var (resetArg, args) = skillContext.Arguments.FindAndRemove(a => a.Value == "--reset");

        // Read remaining arguments, if any
        // There are two arguments provided by any invocations:
        // * Choice - This is the index of the choice being selected
        // * Generation - A unique value that is stored in the brain AND provided by any interactions.
        //                Resetting the story causes a new generation to be created, meaning old interactions won't work.
        var (choiceArg, generationArg) = args;

        // If we aren't provided a generation, generate one.
        var generation = generationArg.Value is { Length: > 0 }
            ? generationArg.Value
            : Guid.NewGuid().ToString("N");

        Log.RunningStory(generation);

        var reset = resetArg.Value is "--reset";

        // Load up any state
        dynamic? state = await Brain.GetAsync(StateKey, Bot.Scope, Bot.ContextId);
        var stateGeneration = await Brain.GetAsAsync<string?>(GenerationKey, Bot.Scope, Bot.ContextId);
        var lastMessageId = await Brain.GetAsAsync<string?>(ActiveMessageIdKey, Bot.Scope, Bot.ContextId);

        var lastMessageTarget = lastMessageId is { Length: > 0 }
            ? new MessageTarget(
                new ChatAddress(skillContext.Room.Address.Type,
                    skillContext.Room.Address.Id,
                    MessageId: lastMessageId))
            : null;

        try
        {
            // we load all our state from the brain, so reset any lingering stuff in memory
            _story.ResetState();

            // If we were invoked from chat, or with an explicit '--reset' flag, reset the state.
            // In theory, reset IMPLIES !skillContext.IsInteraction because you can't pass the '--reset' arg via an interaction
            // But we'll check for that anyway, just in case.
            if (!skillContext.IsInteraction || reset)
            {
                Log.ResettingStoryState();

                // If there's an active message ID, try to clean it up
                if (lastMessageTarget is not null)
                {
                    Log.ReplyingOnMessage(lastMessageTarget.ToString() ?? "<null>");
                    await Bot.ReplyAsync("This message has expired.",
                        new MessageOptions
                        {
                            To = lastMessageTarget
                        });
                }

                lastMessageTarget = null;

                await Brain.DeleteAsync(StateKey, Bot.Scope, Bot.ContextId);
                await Brain.DeleteAsync(ActiveMessageIdKey, Bot.Scope, Bot.ContextId);
                await Brain.DeleteAsync(GenerationKey, Bot.Scope, Bot.ContextId);
            }
            else if (skillContext.IsInteraction)
            {
                // Someone interacted with us, so we need to load state
                // Load state from the brain.
                if (state is null)
                {
                    // The user clicked an interaction, but there is no state!
                    // If possible, reply on the (out of date) last active message.
                    Log.StateNotFound();

                    // Don't say anything. The user clicked an old button and we don't know what to do about it.
                    return null;
                }

                _story.state.LoadJson(state.ToString());

                // Verify the active generation
                if (stateGeneration is null || generation != stateGeneration)
                {
                    Log.GenerationOutOfDate(stateGeneration ?? string.Empty, generation);

                    // Don't say anything. The user clicked an old button and we don't know what to do about it.
                    return null;
                }
            }

            IMessageTarget? target = null;
            if (lastMessageTarget is not null)
            {
                target = lastMessageTarget;
            }
            else if (Bot is { Scope: SkillDataScope.Conversation or SkillDataScope.User })
            {
                target = skillContext.Thread;
            }

            // Now that we've loaded state, delete it all.
            // Why? Because if we fail we want to cleanly reset state.
            // So we wipe it all out until we've successfully progressed the story.
            await Brain.DeleteAsync(StateKey, Bot.Scope, Bot.ContextId);
            await Brain.DeleteAsync(ActiveMessageIdKey, Bot.Scope, Bot.ContextId);
            await Brain.DeleteAsync(GenerationKey, Bot.Scope, Bot.ContextId);

            var options = new MessageOptions
            {
                To = target
            };

            var output = new StringBuilder();

            if (choiceArg.Value is { Length: > 0 } && int.TryParse(choiceArg.Value, out var choice))
            {
                _story.ChooseChoiceIndex(choice);
            }

            while (_story.canContinue)
            {
                output.Append(_story.Continue());
            }

            var choices = _story.currentChoices.Select(x => new Button(x.text, $"{x.index} {generation}")).ToArray();
            if (choices.Length > 0)
            {
                Log.ReplyingOnMessage(options.To?.ToString() ?? "<null>");
                var ret = await Bot.ReplyWithButtonsAsync(output.ToString(), choices, options);
                if (ret.Success && ret.MessageId is not null)
                {
                    await Brain.WriteAsync(ActiveMessageIdKey, ret.MessageId, Bot.Scope, Bot.ContextId);
                }
            }
            else
            {
                if (lastMessageTarget is not null)
                {
                    Log.ReplyingOnMessage(lastMessageTarget.ToString() ?? "<null>");
                    await Bot.ReplyAsync(output.ToString(),
                        new MessageOptions
                        {
                            To = lastMessageTarget
                        });
                }
                else
                {
                    Log.ReplyingOnMessage(options.To?.ToString() ?? "<null>");
                    await Bot.ReplyAsync(output.ToString(), options);
                }
            }

            if (_story.state.didSafeExit)
            {
                Log.StoryConcluded();
                // No need to clean up, we did it above.
                _story.ResetState();
            }
            else
            {
                Log.StorySuspended();
                // We cleaned out the state and generation, so save them again.
                await Brain.WriteAsync(StateKey, _story.state.ToJson(), Bot.Scope, Bot.ContextId);
                await Brain.WriteAsync(GenerationKey, generation, Bot.Scope, Bot.ContextId);
            }
        }
        catch (Exception e)
        {
            // In the event of an exception, clear any state and reset.
            return e;
        }

        return null;
    }

    public string Name { get; }
}

static partial class InkScriptLoggerExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Resetting Story State")]
    public static partial void ResettingStoryState(this ILogger<CompiledInkScript> logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "State not found.")]
    public static partial void StateNotFound(this ILogger<CompiledInkScript> logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message =
            "The current generation {CurrentGeneration} does not match the interaction generation {InteractionGeneration}.")]
    public static partial void GenerationOutOfDate(this ILogger<CompiledInkScript> logger, string currentGeneration,
        string interactionGeneration);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "Running story generation {CurrentGeneration}")]
    public static partial void RunningStory(this ILogger<CompiledInkScript> logger, string currentGeneration);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Replying on message {TargetMessageId}")]
    public static partial void ReplyingOnMessage(this ILogger<CompiledInkScript> logger, string targetMessageId);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Information,
        Message = "Story concluded.")]
    public static partial void StoryConcluded(this ILogger<CompiledInkScript> logger);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Story suspended.")]
    public static partial void StorySuspended(this ILogger<CompiledInkScript> logger);
}
