using Abbot.Common.TestHelpers;
using Abbot.Common.TestHelpers.Fakes;
using MassTransit;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Actions;

public class ContinueIfActionTests
{
    public class TheExecuteStepAsyncMethod
    {
        [Theory]
        [InlineData("Hello", "Contains", "Hello", StepOutcome.Succeeded)]
        [InlineData("Hello", "StartsWith", "Hel", StepOutcome.Succeeded)]
        [InlineData("""["Enterprise","Paid"]""", "Contains", "Enterprise", StepOutcome.Succeeded)]
        [InlineData("""["SMB","Paid"]""", "Contains", "Enterprise", StepOutcome.CompletePlaybook)]
        [InlineData("[]", "Contains", "Enterprise", StepOutcome.CompletePlaybook)]
        [InlineData("", "Contains", "Test", StepOutcome.CompletePlaybook)]
        [InlineData("Hello", "StartsWith", "Byte", StepOutcome.CompletePlaybook)]
        [InlineData("20", "GreaterThan", "30", StepOutcome.CompletePlaybook)]
        [InlineData("30", "GreaterThan", "20", StepOutcome.Succeeded)]
        public async Task ReturnsSuccessWhenConditionMet(string left, string comparison, string right, StepOutcome expected)
        {
            var env = TestEnvironment.Create();
            var playbook = await env.CreatePlaybookAsync();
            var executor = new SystemContinueIfAction.Executor();
            var consumeContext = Substitute.For<ConsumeContext>();
            var stepContext = new StepContext(consumeContext)
            {
                ActionReference = new("seq-1", "action-1", 0),
                Step = new ActionStep("action-1", "some-action"),
                Inputs = new Dictionary<string, object?>
                {
                    ["left"] = left,
                    ["comparison"] = comparison,
                    ["right"] = right,
                },
                Playbook = playbook,
                PlaybookRun = new PlaybookRun
                {
                    Playbook = playbook,
                    Version = 1,
                    State = "",
                    SerializedDefinition = "",
                    Properties = new()
                    {
                        ActivityId = "<unknown>"
                    }
                },
                TemplateEvaluator = new FakeTemplateEvaluator(),
            };

            var result = await executor.ExecuteStepAsync(stepContext);

            Assert.Equal(expected, result.Outcome);
        }
    }
}
