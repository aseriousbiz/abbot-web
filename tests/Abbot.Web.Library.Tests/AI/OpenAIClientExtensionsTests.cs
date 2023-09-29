using Azure.AI.OpenAI;
using Serious.Abbot.AI;
using static Serious.ReflectionExtensions;

public class OpenAIClientExtensionsTests
{
    public class TheToCompletionResultMethod
    {
        [Fact]
        public void CreatesCompletionResultFromCompletions()
        {
            var choices = new[]
            {
                Instantiate<Choice>(
                    "the resulting text",
                    (int?)0.7,
                    Instantiate<CompletionsLogProbability>(),
                    "stop")
            };
            var usage = Instantiate<CompletionsUsage>(200, 123, 323);
            var completion = Instantiate<Completions>(
                "some-id",
                (int?)123245,
                "text-davinci-003",
                choices,
                usage);

            var result = completion.ToCompletionResult();

            Assert.Equal("the resulting text", result.Completions[0].Text);
            Assert.Equal("text-davinci-003", result.Model);
            Assert.Equal("some-id", result.Id);
            Assert.Equal(200, result.Usage.CompletionTokens);
            Assert.Equal(123, result.Usage.PromptTokens);
            Assert.Equal(323, result.Usage.TotalTokens);
        }
    }
}
