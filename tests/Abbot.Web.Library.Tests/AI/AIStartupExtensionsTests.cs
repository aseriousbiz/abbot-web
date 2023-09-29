using Abbot.Common.TestHelpers;
using Serious.Abbot.AI.Commands;

namespace Abbot.Web.Library.Tests.AI;

public class AIStartupExtensionsTests
{
    [Fact]
    public void RegistersBuiltInCommands()
    {
        var env = TestEnvironment.Create();
        var registry = env.Get<CommandRegistry>();

        // I don't believe it's necessary to assert _all_ the commands are here.
        // But we should assert a few.
        Assert.Contains(registry.GetAllCommands(), c => c.Name == "system.noop");
        Assert.Contains(registry.GetAllCommands(), c => c.Name == "rem.get");
    }
}
