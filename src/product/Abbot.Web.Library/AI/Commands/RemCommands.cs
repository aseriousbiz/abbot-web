using System.Collections.Generic;
using System.Linq;

namespace Serious.Abbot.AI.Commands;

[Command(
    "rem.search",
    "Searches the Rem database for a key matching ANY of the provided terms. Include MANY POSSIBLE terms to improve search results. Returns ALL matching keys and their values.",
    Exemplar = """
    {
        "terms": ["term1", "term2"]
    }
    """)]
public record RemSearchCommand() : Command("rem.search")
{
    public required IReadOnlyList<string> Terms { get; init; }

    public override string ToString() =>
        $"rem.search([{string.Join(", ", Terms.Select(t => $"\"{t}\""))}])";
}

[Command(
    "rem.get",
    "Reads the key in the Rem database that matches the provided key EXACTLY. You MUST have identified the correct key using a prior `rem.search` command.",
    Exemplar = """
    {
        "key": "key1"
    }
    """)]
public record RemGetCommand() : Command("rem.get")
{
    public required string Key { get; init; }

    public override string ToString() =>
        $"""rem.get("{Key}")""";
}

[Command(
    "rem.set",
    "Stores the provided value in the Rem database at the provided key",
    Exemplar = """
    {
        "key": "key1",
        "value": "val"
    }
    """)]
public record RemSetCommand() : Command("rem.set")
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    public override string ToString() =>
        $"rem.set(\"{Key}\", \"{Value}\")";
}

[Command(
    "rem.forget",
    "Deletes the record at the provided key from the Rem database",
    Exemplar = """
    {
        "key": "key1"
    }
    """)]
public record RemForgetCommand() : Command("rem.forget")
{
    public required string Key { get; init; }

    public override string ToString()
        => $"rem.forget(\"{Key}\")";
}
