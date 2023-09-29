using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Serious.Abbot.Playbooks;
using Serious.TestHelpers;

public class TemplateEvaluatorTests
{
    public class TheEvaluateMethod
    {
        [Theory]
        [InlineData("{{???}}", "Template error: Reached unparseable token in expression: ???}}\n\nOccured at: 0:7\n\nTemplate:\n{{???}}")]
        [InlineData("{{foo | bar}}", "Template error: blockParams definition has incorrect syntax\n\nOccured at: 0:6\n\nTemplate:\n{{foo | bar}}")]
        [InlineData("{{/unless}}", "Template error: A closing element '/unless' was found without a matching open element\n\nTemplate:\n{{/unless}}")]
        public void RethrowsAsValidationException(string template, string expected)
        {
            var inputTemplateContext = new InputTemplateContext
            {
                Steps = new Dictionary<string, TemplateStepResult>(),
                Previous = null,
                Trigger = null,
                Outputs = new Dictionary<string, object?>
                {
                    ["channel"] = new { id = "C0123456", name = "The Room" }
                }
            };
            var evaluator = new TemplateEvaluator(inputTemplateContext, new TagList());

            var ex = Assert.Throws<ValidationException>(() => evaluator.Evaluate(template));

            Assert.Equal(expected, ex.Message.NormalizeLineEndings());
        }

        [Theory]
        [InlineData("{{ outputs.channel.id }}", "C0123456")]
        [InlineData("{{ outputs.channel.name }}", "The Room")]
        public void CanEvaluateTemplates(string template, string expected)
        {
            var inputTemplateContext = new InputTemplateContext
            {
                Steps = new Dictionary<string, TemplateStepResult>(),
                Previous = null,
                Trigger = null,
                Outputs = new Dictionary<string, object?>
                {
                    ["channel"] = new { id = "C0123456", name = "The Room" }
                }
            };
            var evaluator = new TemplateEvaluator(inputTemplateContext, new TagList());

            var result = evaluator.Evaluate(template);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("{{outputs.channel}}")]
        [InlineData("{{  outputs.channel  }}")]
        [InlineData("{{ trigger.outputs.channel }}")]
        public void CanEvaluateObjects(string expression)
        {
            var channel = new {
                id = "C0123456",
                name = "The Room"
            };
            var inputTemplateContext = new InputTemplateContext
            {
                Steps = new Dictionary<string, TemplateStepResult>(),
                Previous = null,
                Trigger = new TemplateStepResult
                {
                    Id = "Someid",
                    Outcome = StepOutcome.Succeeded,
                    Outputs = new Dictionary<string, object?>
                    {
                        ["channel"] = channel,
                    },
                    Problem = null
                },
                Outputs = new Dictionary<string, object?>
                {
                    ["channel"] = channel,
                }
            };
            var evaluator = new TemplateEvaluator(inputTemplateContext, new TagList());

            var result = evaluator.Evaluate(expression);

            Assert.Same(channel, result);
        }
    }
}
