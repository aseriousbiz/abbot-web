using Serious.Abbot.AI.Commands;

namespace Abbot.Web.Library.Tests.AI.Commands;

[Command("test.noop", "Test command with no args.", Exemplar = """{}""")]
public record TestNoopCommand() : Command("test.noop");

[Command("test.args", "Test command with args.")]
public record TestArgsCommand() : Command("test.args")
{
    public required string StringArg { get; init; }
    public int IntArg { get; init; } = 99;
    public required DateTime DateArg { get; init; }
    public IList<int> ListArg { get; init; } = new List<int>() { 9 };
}
