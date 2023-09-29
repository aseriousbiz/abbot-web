using Abbot.Common.TestHelpers;
using Azure.AI.TextAnalytics;
using OpenAI_API.Models;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Services;

namespace Abbot.Web.Library.Tests.AI;

public class ArgumentRecognizerTests
{
    public class TheRecognizeArgumentsAsyncMethod
    {
        [Theory]
        [InlineData(""""
"""
The Arguments
"""
"""",
            "The Arguments",
            7)]
        [InlineData("The Arguments", "The Arguments", 3)]
        public async Task GeneratesAppropriateChatGptPromptAndProcessesResult(
            string completion,
            string arguments,
            int completionTokenCount)
        {
            var env = TestEnvironment.Create();
            env.TextAnalyticsClient.AddResult("Check benefits for SSN 555-12-1234",
                "Check benefits for SSN ***-**-****",
                new[]
                {
                    new SensitiveValue("555-12-1234", PiiEntityCategory.USSocialSecurityNumber, null, 1.0, 23, 11)
                });

            await env.AISettings.SetModelSettingsAsync(
                AIFeature.ArgumentRecognizer,
                new ModelSettings
                {
                    Model = Model.ChatGPTTurbo,
                    Prompt = new()
                    {
                        Version = PromptVersion.Version1,
                        Text =
                            """
                            SKILLNAME:{SkillName}
                            """,
                    },
                    Temperature = 1.0
                },
                env.TestData.Member);

            const string expectedPrompt = """"
system: SKILLNAME:hr

user: Check benefits for SSN 123-45-6789

assistant: topic=benefits ssn=123-45-6789

user: Find employment status for employee 123

assistant: topic=employment-status id=123

user: Check benefits for SSN 999-00-0001

"""";

            env.OpenAiClient.PushChatResult(completion);

            var skill = await env.CreateSkillAsync("hr");
            skill.Exemplars.Add(new()
            {
                Skill = skill,
                SkillId = skill.Id,
                Exemplar = "Check benefits for SSN 123-45-6789",
                Properties = new()
                {
                    Arguments = "topic=benefits ssn=123-45-6789",
                }
            });

            skill.Exemplars.Add(new()
            {
                Skill = skill,
                SkillId = skill.Id,
                Exemplar = "Find employment status for employee 123",
                Properties = new()
                {
                    Arguments = "topic=employment-status id=123",
                }
            });

            var recognizer = env.Activate<ArgumentRecognizer>();

            var result = await recognizer.RecognizeArgumentsAsync(
                skill,
                skill.Exemplars,
                "Check benefits for SSN 555-12-1234",
                env.TestData.Member);

            Assert.Equal(arguments, result.Arguments);
            Assert.Equal(expectedPrompt, result.Prompt.Reveal());
            Assert.Equal(53, result.TokenUsage.PromptTokenCount);
            Assert.Equal(completionTokenCount, result.TokenUsage.CompletionTokenCount);

            var receivedPrompt = Assert.Single(env.OpenAiClient.ReceivedChatPrompts);
            Assert.Equal(expectedPrompt, receivedPrompt.Format());
        }
    }
}
