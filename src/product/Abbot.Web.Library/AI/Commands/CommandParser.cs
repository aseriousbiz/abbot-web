using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.AI.Commands;

public class CommandParser
{
    public static readonly JsonSerializerSettings BaseSettings = new JsonSerializerSettings()
    {
        TypeNameHandling = TypeNameHandling.None,
        DefaultValueHandling = DefaultValueHandling.Include,
        Formatting = Formatting.None,
        ContractResolver = new AbbotContractResolver(),
    };

    public CommandRegistry Registry { get; }

    readonly JsonSerializer _serializer;

    public CommandParser(CommandRegistry registry)
    {
        Registry = registry;
        _serializer = JsonSerializer.Create(BaseSettings);
        _serializer.Converters.Add(new CommandConverter(registry));
    }

    /// <summary>
    /// Parses a list of commands from the provided JSON.
    /// </summary>
    /// <param name="json">The JSON to parse</param>
    public Reasoned<CommandList>? ParseCommands(string json) =>
        Reasoned.ParseJson<CommandList>(json, _serializer).Require();
}
