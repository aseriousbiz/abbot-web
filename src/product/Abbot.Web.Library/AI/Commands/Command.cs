using System.Collections.Generic;
using Newtonsoft.Json;

namespace Serious.Abbot.AI.Commands;

/// <summary>
/// Describes an AbbotLang "Command".
/// </summary>
/// <remarks>
/// Commands are the interface from the Language Model back to Abbot.
/// The Language Model understands natural language.
/// Abbot does not.
/// So Abbot asks the Language Model to express its intent in terms of Commands.
/// </remarks>
public abstract record Command([property: JsonProperty("command")] string Name);

/// <summary>
/// Represents an unknown command
/// </summary>
public record UnknownCommand(string Name) : Command(Name)
{
    [JsonExtensionData]
    public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>();
}

[Command("system.noop", "Directs Abbot to take no action.")]
public record NoopCommand() : Command("system.noop")
{
    public override string ToString()
        => "system.noop()";
}
