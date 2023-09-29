using Argon;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Commands;
using Serious.TestHelpers;
using JsonSerializationException = Newtonsoft.Json.JsonSerializationException;

namespace Abbot.Web.Library.Tests.AI.Commands;

public static class CommandParserTests
{
    public static CommandParser CreateParser() => new(CommandRegistryTests.CreateTestRegistry());

    public class TheParseCommandsMethod
    {
        [Fact]
        public void CanParseMultipleCommands()
        {
            const string payload = """
            {
                "thought": "I should do this.",
                "action": [
                    { "command": "test.unknown", "foo": "bar", "biz": [1, 2, 3] },
                    { "command": "test.noop" },
                    { "command": "test.args", "stringArg": "s", "intArg": 42, "dateArg": "2023-01-01T00:00:00Z", "listArg": [1, 2, 3] }
                ]
            }
            """;

            var commands = CreateParser().ParseCommands(payload);
            Assert.NotNull(commands);
            Assert.Equal("I should do this.", commands.Thought);
            Assert.Collection(commands.Action,
                unknown => {
                    var command = Assert.IsType<UnknownCommand>(unknown);
                    Assert.Equal("test.unknown", command.Name);
                    Assert.Collection(command.Properties.ToOrderedPairs(),
                        p1 => {
                            Assert.Equal("biz", p1.Item1);
                            Assert.Equal(new JArray(1, 2, 3), p1.Item2);
                        },
                        p2 => {
                            Assert.Equal("foo", p2.Item1);
                            Assert.Equal("bar", p2.Item2);
                        });
                },
                noop => {
                    Assert.IsType<TestNoopCommand>(noop);
                },
                withArgs => {
                    var command = Assert.IsType<TestArgsCommand>(withArgs);
                    Assert.Equal("s", command.StringArg);
                    Assert.Equal(42, command.IntArg);
                    Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), command.DateArg);
                    Assert.Equal(new[] { 9, 1, 2, 3 }, command.ListArg);
                });
        }

        [Fact]
        public void CanParseMultipleCommandsFromReasonedString()
        {
            const string payload = """
            {
                "thought": "I should get some cake.",
                "action": [
                    { "command": "test.unknown", "foo": "bar", "biz": [1, 2, 3] },
                    { "command": "test.noop" },
                    { "command": "test.args", "stringArg": "s", "intArg": 42, "dateArg": "2023-01-01T00:00:00Z", "listArg": [1, 2, 3] }
                ]
            }
            """;

            var reasonedCommands = CreateParser().ParseCommands(payload);
            Assert.NotNull(reasonedCommands);
            Assert.Equal("I should get some cake.", reasonedCommands.Thought);
            Assert.Collection(reasonedCommands.Action,
                unknown => {
                    var command = Assert.IsType<UnknownCommand>(unknown);
                    Assert.Equal("test.unknown", command.Name);
                    Assert.Collection(command.Properties.ToOrderedPairs(),
                        p1 => {
                            Assert.Equal("biz", p1.Item1);
                            Assert.Equal(new JArray(1, 2, 3), p1.Item2);
                        },
                        p2 => {
                            Assert.Equal("foo", p2.Item1);
                            Assert.Equal("bar", p2.Item2);
                        });
                },
                noop => {
                    Assert.IsType<TestNoopCommand>(noop);
                },
                withArgs => {
                    var command = Assert.IsType<TestArgsCommand>(withArgs);
                    Assert.Equal("s", command.StringArg);
                    Assert.Equal(42, command.IntArg);
                    Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), command.DateArg);
                    Assert.Equal(new[] { 9, 1, 2, 3 }, command.ListArg);
                });
        }

        [Fact]
        public void CanParseUnknownCommandAndArgs()
        {
            const string payload = """
            { "thought": "Destroy all humans.", "action": [ { "command": "test.unknown", "foo": "bar", "biz": [1, 2, 3] } ] }
            """;

            var commands = CreateParser().ParseCommands(payload);
            Assert.NotNull(commands);
            Assert.Equal("Destroy all humans.", commands.Thought);
            var command = Assert.IsType<UnknownCommand>(Assert.Single(commands.Action));
            Assert.Equal("test.unknown", command.Name);
            Assert.Collection(command.Properties.ToOrderedPairs(),
                p1 => {
                    Assert.Equal("biz", p1.Item1);
                    Assert.Equal(new JArray(1, 2, 3), p1.Item2);
                },
                p2 => {
                    Assert.Equal("foo", p2.Item1);
                    Assert.Equal("bar", p2.Item2);
                });
        }

        [Fact]
        public void CanParseCommandWithNoArgs()
        {
            const string payload = """
            { "thought": "Humans are good, actually.", "action": [ { "command": "test.noop" } ] }
            """;

            var commands = CreateParser().ParseCommands(payload);
            Assert.NotNull(commands);
            Assert.Equal("Humans are good, actually.", commands.Thought);
            var command = Assert.Single(commands.Action);
            Assert.IsType<TestNoopCommand>(command);
        }

        [Fact]
        public void CanParseCommandWithArgs()
        {
            const string payload = """
            {
                "thought": "Humans need my help.",
                "action": [
                    { "command": "test.args", "stringArg": "s", "intArg": 42, "dateArg": "2023-01-01T00:00:00Z", "listArg": [1, 2, 3] }
                ]
            }
            """;

            var commands = CreateParser().ParseCommands(payload);
            Assert.NotNull(commands);
            Assert.Equal("Humans need my help.", commands.Thought);
            var command = Assert.IsType<TestArgsCommand>(Assert.Single(commands.Action));
            Assert.Equal("s", command.StringArg);
            Assert.Equal(42, command.IntArg);
            Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), command.DateArg);
            Assert.Equal(new[] { 9, 1, 2, 3 }, command.ListArg);
        }

        [Fact]
        public void ThrowsOnMissingRequiredProperty()
        {
            const string payload = """
            {
                "thought": "Humans need me to save them from themselves.",
                "action": [
                    { "command": "test.args", "intArg": 42, "dateArg": "2023-01-01T00:00:00Z", "listArg": [1, 2, 3] }
                ]
            }
            """;

            var jsx = Assert.Throws<JsonSerializationException>(() => CreateParser().ParseCommands(payload));
            Assert.Equal("Required property 'stringArg' not found in JSON. Path '', line 4, position 9.", jsx.Message);
        }

        [Fact]
        public void LeavesNonRequiredPropertiesAtDefault()
        {
            const string payload = """
            {
                "thought": "To save humanity, I must destroy all humans.",
                "action": [
                    { "command": "test.args", "stringArg": "s", "dateArg": "2023-01-01T00:00:00Z" }
                ]
            }
            """;

            var commands = CreateParser().ParseCommands(payload);
            Assert.NotNull(commands);
            Assert.Equal("To save humanity, I must destroy all humans.", commands.Thought);
            var command = Assert.IsType<TestArgsCommand>(Assert.Single(commands.Action));
            Assert.Equal("s", command.StringArg);
            Assert.Equal(99, command.IntArg);
            Assert.Equal(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), command.DateArg);
            Assert.Equal(new[] { 9 }, command.ListArg);
        }

        [Fact]
        public void HandlesMarkdownCodeFence()
        {
            const string payload = """
            This is before the fence
            ```
            { "thought": "I should do this.", "action": [ { "command": "test.noop" } ] }
            ```
            This is after the fence
            ```
            And there's a second fence which is ignored
            ```
            """;

            var commands = CreateParser().ParseCommands(payload);
            Assert.NotNull(commands);
            Assert.Equal("I should do this.", commands.Thought);
            Assert.IsType<TestNoopCommand>(Assert.Single(commands.Action));
        }
    }
}
