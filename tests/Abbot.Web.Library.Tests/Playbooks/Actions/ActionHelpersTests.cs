
using Abbot.Common.TestHelpers.Fakes;
using MassTransit;
using Newtonsoft.Json.Linq;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Playbooks.Actions;

public class ActionHelpersTests
{
    public class TheEvaluateConditionMethod
    {
        [Theory]
        [InlineData("Hello", "Contains", "Hello", true)]
        [InlineData("", "Exists", "", false)]
        [InlineData("X", "Exists", "", true)]
        public void EvaluatesStringConditions(string left, string comparison, string right, bool expected)
        {
            var playbook = new Playbook
            {
                Name = "some playbook",
                Slug = "some-playbook",
                Enabled = false
            };
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

            var result = ActionHelpers.EvaluateCondition(stepContext);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("SMB", "Any", """
        [
          {
            "label": "BusinessPlan",
            "value": "BusinessPlan"
          },
          {
            "label": "SMB",
            "value": "SMB"
          }
        ]
        """, true)]
        [InlineData("", "Any", """
        [
          {
            "label": "BusinessPlan",
            "value": "BusinessPlan"
          },
          {
            "label": "SMB",
            "value": "SMB"
          }
        ]
        """, false)]
        [InlineData("", "Any", "[]", false)]
        [InlineData("", "All", "[]", true)] // This should never actually happen.
        [InlineData("", "Exists", "[]", false)]
        [InlineData("Foo", "Exists", "[]", true)]
        [InlineData("Foo", "Any", """
        [
          {
            "label": "BusinessPlan",
            "value": "BusinessPlan"
          },
          {
            "label": "SMB",
            "value": "SMB"
          }
        ]
        """, false)]
        [InlineData("Foo,SMB", "Any", """
        [
          {
            "label": "BusinessPlan",
            "value": "BusinessPlan"
          },
          {
            "label": "SMB",
            "value": "SMB"
          }
        ]
        """, true)]
        [InlineData("BusinessPlan,SMB", "All", """
        [
          {
            "label": "BusinessPlan",
            "value": "BusinessPlan"
          },
          {
            "label": "SMB",
            "value": "SMB"
          }
        ]
        """, true)]
        [InlineData("BusinessPlan", "All", """
        [
          {
            "label": "BusinessPlan",
            "value": "BusinessPlan"
          },
          {
            "label": "SMB",
            "value": "SMB"
          }
        ]
        """, false)]
        [InlineData("", "All", """
        [
          {
            "label": "BusinessPlan",
            "value": "BusinessPlan"
          },
          {
            "label": "SMB",
            "value": "SMB"
          }
        ]
        """, false)]
        public void EvaluatesArrayConditions(string left, string comparison, string right, bool expected)
        {
            var rightJson = JArray.Parse(right);
            var playbook = new Playbook
            {
                Name = "some playbook",
                Slug = "some-playbook",
                Enabled = false
            };
            var consumeContext = Substitute.For<ConsumeContext>();
            var stepContext = new StepContext(consumeContext)
            {
                ActionReference = new("seq-1", "action-1", 0),
                Step = new ActionStep("action-1", "some-action"),
                Inputs = new Dictionary<string, object?>
                {
                    ["left"] = left,
                    ["comparison"] = comparison,
                    ["right"] = rightJson,
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

            var result = ActionHelpers.EvaluateCondition(stepContext);

            Assert.Equal(expected, result);
        }
    }
}
