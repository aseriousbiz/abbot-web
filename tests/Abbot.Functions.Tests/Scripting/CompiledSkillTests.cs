using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.TestHelpers;
using Xunit;
using Xunit.Abstractions;

public class CompiledSkillTests
{
    public class TheRunAsyncMethod
    {
        readonly ITestOutputHelper _output;

        public TheRunAsyncMethod(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExecutesTheCompiledScript()
        {
            const string code = "await Bot.ReplyAsync(\"test\");";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var context = new FakeBot();

            await compiledSkill.RunAsync(context);

            Assert.Equal("test", context.Replies.Single());
        }

        [Fact]
        public async Task RunsAsyncCode()
        {
            const string code = @"await Task.Delay(TimeSpan.FromSeconds(0.1));
await Bot.ReplyAsync(Bot.Arguments.Value);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsAsyncCode)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal("The args", bot.Replies.Single());
        }

        [Fact]
        public async Task ReturnsExceptionFromScript()
        {
            const string code = @"throw new ArgumentException(""Your argument is invalid."");";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsAsyncCode)
            };

            var exception = await compiledSkill.RunAsync(bot);
            Assert.Equal("Your argument is invalid.", exception?.Message);
        }

        [Fact]
        public async Task RunsMethodWithSingleReply()
        {
            const string code = "await Bot.ReplyAsync(Bot.Arguments.Value);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal("The args", bot.Replies.Single());
        }

        [Fact]
        public async Task RunsMethodWithMultipleReplies()
        {
            const string code = "await Bot.ReplyAsync(\"one\");" +
                                "await Bot.ReplyAsync(\"two\");";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal(2, bot.Replies.Count);
            Assert.Equal("one", bot.Replies[0]);
            Assert.Equal("two", bot.Replies[1]);
        }

        [Fact]
        public async Task RunsDynamicCode()
        {
            const string code = @"dynamic msg = Bot;
        await Bot.ReplyAsync((string)msg.Arguments.Value);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal("The args", bot.Replies.Single());
        }

        [Fact]
        public async Task CanStoreAndRetrieveStrings()
        {
            const string code =
@"await Bot.Brain.WriteAsync(""some-key"", ""args: "" + Bot.Arguments.Value);
var args = await Bot.Brain.GetAsync(""some-key"");
await Bot.ReplyAsync(args as string);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal("args: The args", bot.Replies.Single());
        }

        [Fact]
        public async Task CanRetrieveSecrets()
        {
            const string code =
                @"var secret = await Bot.Secrets.GetAsync(""some-secret"");
await Bot.ReplyAsync(secret);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var secrets = new FakeSecrets();
            secrets.AddSecret("some-secret", "The cake is a lie.");
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name), secrets)
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal("The cake is a lie.", bot.Replies.Single());
        }

        [Fact]
        public async Task CanStoreAndRetrieveObjects()
        {
            const string code = @"
public record Item {
  public string Name {get; }
  public Item(string name) => Name = name;
}
var item = new Item(Bot.Arguments.Value);
await Bot.Brain.WriteAsync(""some-key"", item);
var stored = await Bot.Brain.GetAsync(""some-key"");
await Bot.ReplyAsync(""args: "" + stored.Name);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal("args: The args", bot.Replies.Single());
        }

        [Fact]
        public async Task CanStoreAndRetrieveObjectsStatically()
        {
            const string code = @"
public class Item {
  public string Name {get; set;}
}
var item = new Item { Name = Bot.Arguments.Value };
await Bot.Brain.WriteAsync(""some-key"", item);
var stored = await Bot.Brain.GetAsAsync<Item>(""some-key"");
await Bot.ReplyAsync(""args: "" + stored.Name);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            var reply = Assert.Single(bot.Replies);
            Assert.Equal("args: The args", reply);
        }

        [Fact]
        public async Task CanStoreAndRetrieveObjectsWithFuzzyFilter()
        {
            const string code = @"
public class Item {
  public string Name {get; set;}
}
await Bot.Brain.WriteAsync(""key1"", new Item { Name = ""val1"" });
await Bot.Brain.WriteAsync(""key2"", new Item { Name = ""val2"" });
await Bot.Brain.WriteAsync(""fred"", new Item { Name = ""val3"" });
var stored = await Bot.Brain.GetAllAsync(""key"");
await Bot.ReplyAsync(stored.Count + "" "" + stored[0].Value.Name + "" "" + stored[1].Value.Name);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            var reply = Assert.Single(bot.Replies);
            Assert.Equal("2 val1 val2", reply);
        }

        [Fact]
        public async Task CanStoreAndRetrieveAnonymousObjectDynamically()
        {
            const string code = @"
await Bot.Brain.WriteAsync(""some-key"", new { Arg = Bot.Arguments.Value });
var stored = await Bot.Brain.GetAsync(""some-key"");
await Bot.ReplyAsync(stored.Arg);";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            var reply = Assert.Single(bot.Replies);
            Assert.Equal("The args", reply);
        }

        [Fact]
        public async Task CanStoreAndRetrieveAnonymousObjectDynamicallyWithAnotherFunction()
        {
            const string code = @"
await Bot.Brain.WriteAsync(""a-key"", new { Arg = Bot.Arguments.Value });
var stored = await GetStored(Bot.Brain);
await Bot.ReplyAsync(stored.Arg);

public Task<dynamic> GetStored(dynamic storage)
{
    return storage.GetAsync(""a-key"");
}
";
            var compiledSkill = TestSkillCompiler.CompileAsync(code);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments("The args"),
                SkillName = nameof(RunsMethodWithSingleReply)
            };

            await compiledSkill.RunAsync(bot);

            var reply = Assert.Single(bot.Replies);
            Assert.Equal("The args", reply);
        }

        [Fact]
        public async Task CanBuildAStackSkill()
        {
            const string code = @"var (command, value) = Bot.Arguments;

if (command is IMissingArgument || (!(command.Value is ""push"" or ""pop"" or ""list""))) {
    await Bot.ReplyAsync(@""• `@abbot stack push {text}` to push something onto your stack.
• `@abbot stack pop` to pop the last item off your stack.
• `@abbot stack list` to list the items in your stack."");
    return;
}

string stackStorageKy = Bot.From.Id + ""@abbot stack"";
var stack = await Bot.Brain.GetAsync(stackStorageKy) as Stack<string> ?? new Stack<string>();
stack = new Stack<string>(stack);
if (command.Value is ""pop"" or ""list"") {
    if (stack.Count == 0) {
        await Bot.ReplyAsync(""There's nothing in the stack."");
        return;
    }

    if (command.Value is ""pop"") {
        var returnValue = stack.Pop();
        await Bot.Brain.WriteAsync(stackStorageKy, stack);
        await Bot.ReplyAsync($""`{returnValue}`"");
        return;
    }

    await Bot.ReplyAsync(string.Join(""\n"", stack.Select(item => $""• {item}"")));
    return;
}

if (value is IMissingArgument) {
    await Bot.ReplyAsync(""Make sure to specify a value to push on the stack"");
    return;
}
stack.Push(value.Value);
await Bot.Brain.WriteAsync(stackStorageKy, stack);
await Bot.ReplyAsync($""Pushed `{value.Value}` onto the stack. There are now {stack.Count} items in your stack."");";

            var compiledSkill = TestSkillCompiler.CompileAsync(code);

            var argumentsAndResults = new[]
            {
                ("push", "The Arguments", "Pushed `The Arguments` onto the stack. There are now 1 items in your stack."),
                ("push", "More Stuff", "Pushed `More Stuff` onto the stack. There are now 2 items in your stack."),
                ("push", "a third value", "Pushed `a third value` onto the stack. There are now 3 items in your stack."),
                ("pop", "", "`a third value`")
            };

            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                SkillName = "stack",
                Arguments = new Arguments(""),
                From = new PlatformUser { Id = "U123", UserName = "Mia" }
            };
            foreach (var (command, argument, expectedResult) in argumentsAndResults)
            {
                var firstArgument = new Argument(command);
                var secondArgument = argument.Length == 0 ? new MissingArgument() : new Argument(argument);

                bot.Arguments = new Arguments($"{firstArgument} {secondArgument}");
                await compiledSkill.RunAsync(bot);
                Assert.Equal(expectedResult, bot.Replies[^1]);
            }
        }

        [Fact]
        public async Task InkSkillCode()
        {
            const string code = @"Welcome -> END";
            var compiledSkill = await TestSkillCompiler.CompileInkAsync(nameof(InkSkillCode), code, _output);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name))
            {
                Arguments = new Arguments(string.Empty),
                SkillName = nameof(InkSkillCode)
            };

            await compiledSkill.RunAsync(bot);

            Assert.Equal("Welcome", bot.Replies.Single());
        }

        [Fact]
        public async Task InkSupportsSignal()
        {
            const string code = @"~ signal(""signal"", ""args"")
 -> END";
            var compiledSkill = await TestSkillCompiler.CompileInkAsync(nameof(InkSupportsSignal), code, _output);
            var signaler = Substitute.For<ISignaler>();
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name), signaler: signaler)
            {
                Arguments = new Arguments(string.Empty),
                SkillName = nameof(InkSupportsSignal)
            };

            await compiledSkill.RunAsync(bot);

            await signaler.Received(1).SignalAsync("signal", "args");
        }

        [Fact(Skip = "Inconsistent; see #3250")]
        public async Task InkSignalTimeout()
        {
            const string code = @"~ signal(""signal"", ""args"")
 -> END";

            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    SkillName = "source-skill",
                    Arguments = "the original skill args",
                    Room = new PlatformRoom("C01234", "the-clean-room"),
                    SkillUrl = new Uri("https://app.ab.bot/skills/source-skill")
                },
                RunnerInfo = new SkillRunnerInfo
                {
                    SkillId = 42,
                    MemberId = 23,
                }
            };
            var compiledSkill = await TestSkillCompiler.CompileInkAsync(message.SkillInfo.SkillName, code, _output);
            var url = new Uri("https://ab.bot/api/skills/42/signal");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(url, HttpMethod.Post, async () => {
                // this is important to release the initial SendAsync call to an async state
                await Task.Delay(1);
                // now block so the call can timeout
                new ManualResetEventSlim().Wait(4000);
                return new ApiResult();
            });
            var contextAccessor = new SkillContextAccessor
            {
                SkillContext = new SkillContext(message, "FakeApiKey")
            };
            var signaler = new Signaler(apiClient, contextAccessor);
            var bot = new FakeBot(new FakeBotBrain(compiledSkill.Name), signaler: signaler)
            {
                Arguments = new Arguments(string.Empty),
                SkillName = message.SkillInfo.SkillName
            };

            var exception = await compiledSkill.RunAsync(bot);
            Assert.Equal("Timeout sending signal signal", exception?.Message);
        }
    }
}
