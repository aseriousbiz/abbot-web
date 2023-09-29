using System.ComponentModel.DataAnnotations;
using Abbot.Common.TestHelpers;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Outputs;

public class StepContextTests
{
    public class TheGetMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("default")]
        [InlineData(null, "old")]
        [InlineData("default", "old")]
        public async Task ReturnsDefaultForMissingInput(string? defaultValue, string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook);

            Assert.Equal(defaultValue, stepContext.Get<string>("missing", defaultValue, oldName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("default")]
        [InlineData(null, "old")]
        [InlineData("default", "old")]
        public async Task ReturnsCorrectInputType(string? defaultValue /* ignored */, string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var sampleObj = new SampleOutput();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["string"] = "value",
                    ["object"] = JObject.FromObject(sampleObj),
                    ["old"] = new JArray(), // Ignored
                });

            Assert.Equal("value", stepContext.Get<string>("string", defaultValue, oldName));
            Assert.Equal(sampleObj, stepContext.Get<SampleOutput>("object"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("default")]
        [InlineData(null, "old")]
        [InlineData("default", "old")]
        public async Task ReturnsDefaultForIncorrectInputType(string? defaultValue, string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var sampleObj = new SampleOutput();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["string"] = "value",
                    ["object"] = JObject.FromObject(sampleObj),
                    ["old"] = new JArray(), // Also incorrect
                });

            Assert.Equal(defaultValue, stepContext.Get<string>("object", defaultValue, oldName));
            Assert.Null(stepContext.Get<SampleOutput>("string"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("default")]
        public async Task ReturnsOldValueIfNewValueIsIncorrectInputType(string defaultValue)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var sampleObj = new SampleOutput();
            var sampleOld = new SampleOutput();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["string"] = "value",
                    ["object"] = JObject.FromObject(sampleObj),
                    ["old_string"] = "old",
                    ["old_object"] = JObject.FromObject(sampleOld),
                });

            Assert.Equal("old", stepContext.Get<string>("object", defaultValue, "old_string"));
            Assert.Equal(sampleOld, stepContext.Get<SampleOutput>("string", oldName: "old_object"));
        }
    }

    public class TheExpectMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("old")]
        public async Task ThrowsForMissingInput(string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook);

            var ex = Assert.Throws<ValidationException>(() => {
                stepContext.Expect<string>("missing", oldName);
            });
            Assert.Equal("Input 'missing' is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("old")]
        public async Task ReturnsCorrectInputType(string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var sampleObj = new SampleOutput();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["string"] = "value",
                    ["object"] = JObject.FromObject(sampleObj),
                });

            Assert.Equal("value", stepContext.Expect<string>("string", oldName));
            Assert.Equal(sampleObj, stepContext.Expect<SampleOutput>("object"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("old")]
        public async Task ThrowsForIncorrectInputType(string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var sampleObj = new SampleOutput();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["string"] = "value",
                    ["object"] = JObject.FromObject(sampleObj),
                    ["old"] = new JArray(), // Also incorrect
                });

            var ex = Assert.Throws<ValidationException>(() => {
                stepContext.Expect<string>("object", oldName);
            });
            Assert.Equal("Input 'object' must be convertible to String.", ex.Message);

            ex = Assert.Throws<ValidationException>(() => {
                stepContext.Expect<SampleOutput>("string");
            });
            Assert.Equal("Input 'string' must be convertible to SampleOutput.", ex.Message);
        }

        [Fact]
        public async Task ReturnsOldValueIfNewValueIsIncorrectInputType()
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var sampleObj = new SampleOutput();
            var sampleOld = new SampleOutput();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["string"] = "value",
                    ["object"] = JObject.FromObject(sampleObj),
                    ["old_string"] = "old",
                    ["old_object"] = JObject.FromObject(sampleOld),
                });

            Assert.Equal("old", stepContext.Expect<string>("object", "old_string"));
            Assert.Equal(sampleOld, stepContext.Expect<SampleOutput>("string", "old_object"));
        }
    }

    public class TheExpectMessageTargetMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("old")]
        public async Task ThrowsForMissingInput(string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook);

            var ex = Assert.Throws<ValidationException>(() => {
                stepContext.ExpectMessageTarget("missing", oldName);
            });
            Assert.Equal("Input 'missing' is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("channel")]
        public async Task ThrowsForIncorrectInputType(string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["message_target"] = new JArray(),
                    ["channel"] = new JArray(),
                });

            var ex = Assert.Throws<ValidationException>(() => {
                stepContext.ExpectMessageTarget("message_target", oldName);
            });
            Assert.Equal("Input 'message_target' must be convertible to String.", ex.Message);
        }

        [Theory]
        [InlineData("message_target")]
        [InlineData("message_target", "channel")]
        public async Task ReturnsNewOrOldChannel(string name, string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["channel"] = oldName is "channel" ? "C123" : null,
                    ["message_target"] = oldName is "channel" ? null : "C123",
                });

            var (channelId, messageId) = stepContext.ExpectMessageTarget(name, oldName);

            Assert.Equal("C123", channelId);
            Assert.Null(messageId);
        }

        [Fact]
        public async Task ReturnsParsedChannelAndMessageId()
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var expectedMessageId = "1639006311.178500"; // Generator values are invalid

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["channel"] = "C123",
                    ["message_target"] = SlackFormatter.MessageUrl(
                        $"{playbook.Organization.Slug}.slack.com", // Why doesn't Domain use slack.com?
                        "C123",
                        expectedMessageId),
                });

            var (channelId, messageId) = stepContext.ExpectMessageTarget("message_target");

            Assert.Equal("C123", channelId);
            Assert.Equal(expectedMessageId, messageId);
        }
    }

    public class TheExpectMrkdwnMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("old")]
        public async Task ThrowsForMissingInput(string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook);

            var ex = Assert.Throws<ValidationException>(() => {
                stepContext.ExpectMrkdwn("missing", oldName);
            });
            Assert.Equal("Input 'missing' is required.", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("message")]
        public async Task ThrowsForIncorrectInputType(string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["mrkdwn"] = new JArray(),
                    ["message"] = new MessageOutput
                    {
                        Channel = new() { Id = "C4" },
                        Timestamp = env.IdGenerator.GetSlackMessageId(),
                    },
                });

            var ex = Assert.Throws<ValidationException>(() => {
                stepContext.ExpectMrkdwn("mrkdwn", oldName);
            });
            Assert.Equal("Input 'mrkdwn' must be convertible to String.", ex.Message);
        }

        [Theory]
        [InlineData("message")]
        [InlineData("mrkdwn", "message")]
        public async Task ReturnsRenderedMrkdwnForTipTapDocument(string name, string? oldName = null)
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapHandlebarsNode(new TipTapAttributes("trigger.outputs.intro", "An intro")),
                }),
            });

            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["message"] = StepContext.JsonFormat.Deserialize(StepContext.JsonFormat.Serialize(document)),
                    ["mrkdwn"] = "TipTap takes precedence",
                },
                templates: new()
                {
                    ["{{trigger.outputs.intro}}"] = "Some text ",
                });

            var result = stepContext.ExpectMrkdwn("message");

            Assert.Equal("Some text", result);
        }

        [Theory]
        [InlineData("message")]
        [InlineData("mrkdwn", "message")]
        public async Task ReturnsDocumentForPlainTextString(string name, string? oldName = null)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();

            var stepContext = env.CreateStepContext(playbook,
                inputs: new()
                {
                    ["message"] = "This is a message.",
                });

            var result = stepContext.ExpectMrkdwn("message", oldName);

            Assert.Equal("This is a message.", result);
        }
    }

    record SampleOutput(string MyString = "str", int MyNumber = 42, bool MyBoolean = true);
}
