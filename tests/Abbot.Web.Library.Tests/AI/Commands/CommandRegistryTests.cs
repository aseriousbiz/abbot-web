using Newtonsoft.Json;
using Serious.Abbot.AI.Commands;

namespace Abbot.Web.Library.Tests.AI.Commands;

public static class CommandRegistryTests
{
    public static CommandRegistry CreateTestRegistry()
    {
        var registry = new CommandRegistry();
        registry.RegisterCommands(new[]
        {
            typeof(TestNoopCommand),
            typeof(TestArgsCommand),
        });
        return registry;
    }

    public class TheRegisterCommandsMethod
    {
        [Fact]
        public void RegistersCommandInTheRegistry()
        {
            var registry = new CommandRegistry();
            registry.RegisterCommands(new[] { typeof(TestNoopCommand) });
            Assert.Equal(
                new[] { "test.noop" },
                registry.GetAllCommands().Select(c => c.Name).ToArray());
        }

        [Fact]
        public void ThrowsOnDuplicateCommand()
        {
            var registry = new CommandRegistry();
            registry.RegisterCommands(new[] { typeof(TestNoopCommand) });
            var ex = Assert.Throws<InvalidOperationException>(() => registry.RegisterCommands(new[] { typeof(TestNoopCommand) }));
            Assert.Equal("Command 'test.noop' already registered by 'Abbot.Web.Library.Tests.AI.Commands.TestNoopCommand'", ex.Message);
        }
    }

    public class TheTryResolveCommandMethod
    {
        [Fact]
        public void ReturnsFalseIfNoSuchCommand()
        {
            var registry = new CommandRegistry();
            Assert.False(registry.TryResolveCommand("test.noop", out _));
        }

        [Fact]
        public void SucceedsIfCommandExists()
        {
            var registry = new CommandRegistry();
            registry.RegisterCommands(new[] { typeof(TestNoopCommand) });
            Assert.True(registry.TryResolveCommand("test.noop", out var descriptor));
            Assert.Equal("test.noop", descriptor.Name);
            Assert.Equal("Test command with no args.", descriptor.Description);
            Assert.Equal("""{"command":"test.noop"}""", descriptor.Exemplar?.ToString(Formatting.None));
            Assert.Equal(typeof(TestNoopCommand), descriptor.Type);
        }
    }

    public class TheGetAllCommandsMethod
    {
        [Fact]
        public void ReturnsAllCommands()
        {
            var registry = new CommandRegistry();
            registry.RegisterCommands(new[] { typeof(TestNoopCommand), typeof(TestArgsCommand), });
            Assert.Equal(new[]
            {
                ("test.args", "Test command with args.", """{"command":"test.args"}""", typeof(TestArgsCommand)),
                ("test.noop", "Test command with no args.", """{"command":"test.noop"}""", typeof(TestNoopCommand)),
            }, registry.GetAllCommands().Select(d => (d.Name, d.Description, d.Exemplar.ToString(Formatting.None), d.Type)).ToArray());
        }
    }
}
