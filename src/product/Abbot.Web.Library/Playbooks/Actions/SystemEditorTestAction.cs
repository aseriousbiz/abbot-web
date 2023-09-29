using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Playbooks.Actions;

public class SystemEditorTestRequiredAction : SystemEditorTestAction
{
    public override StepType Type => base.Type with
    {
        Name = "system.editor-test-required",
        Presentation = new()
        {
            Label = "Editor Required Test",
            Icon = "fa-pencil",
            Description = "Action with required inputs of various editor types",
        },
        Inputs = base.Type.Inputs
            .Select(i => i with { Required = true })
            .ToList(),
    };
}

public class SystemEditorTestAction : ActionType<SystemEditorTestAction.Executor>
{
    public override StepType Type { get; } = new("system.editor-test", StepKind.Action)
    {
        Category = "system",
        StaffOnly = true,
        Presentation = new()
        {
            Label = "Editor Test",
            Icon = "fa-pencil",
            Description = "Action with inputs of various editor types",
        },
        Inputs =
        {
            new("string", "String", PropertyType.String),
            new("integer", "Integer", PropertyType.Integer),
            new("float", "Float", PropertyType.Float),
            new("boolean", "Boolean", PropertyType.Boolean),
            new("boolean_default", "Boolean w/ Default", PropertyType.Boolean) { Default = true },
            new("duration", "Duration", PropertyType.Duration),
            new("duration_default", "Duration w/ Default", PropertyType.Duration) { Default = "PT2H" },
            new("channel", "Channel", PropertyType.Channel),
            new("channel_multi", "Channels (Multi)", PropertyType.Channels),
            new("message_target", "Message Target", PropertyType.MessageTarget),
            new("member", "Member", PropertyType.Member),
            new("member_multi", "Member (Multi)", PropertyType.Members),
            new("comparison_type", "Comparison Type", PropertyType.ComparisonType),
            new("comparison_type_default", "Comparison Type w/ Default", PropertyType.ComparisonType) { Default = "ExactMatch" },
            new("notification_type", "Notification Type", PropertyType.NotificationType),
            new("notification_type_default", "Notification Type w/ Default", PropertyType.NotificationType ) { Default = "deadline" },
            new("predefined_expression", "Predefined Expression", PropertyType.PredefinedExpression),
            new("schedule", "Schedule", PropertyType.Schedule),
            new("schedule_default", "Schedule w/ Default", PropertyType.Schedule)
            {
                Default = new HourlySchedule(55),
            },
            new("schedule_cron", "Schedule w/ CRON Default", PropertyType.Schedule) { Default = "0 */2 * * *" },
            new("sentiment", "Sentiment (Hidden)", PropertyType.String) { Hidden = true, Default = "doubleplusungood" },
            new("signal", "Signal", PropertyType.Signal),
            new("skill", "Skill", PropertyType.Skill),
            new("customer", "Customer", PropertyType.Customer),
            new("timezone", "Time Zone", PropertyType.Timezone),
            new("slack_mrkdwn", "Slack Mrkdwn", PropertyType.SlackMrkdwn()),
            new("slack_mrkdwn_1", "Slack Mrkdwn (1)", PropertyType.SlackMrkdwn(1)),
            new("poll", "Poll", PropertyType.Poll),
            new("poll_default", "Poll w/ Default", PropertyType.Poll) { Default = "nps-desc" },
        },
    };

    public class Executor : IActionExecutor
    {
        readonly ILogger<Executor> _logger;

        public Executor(ILogger<Executor> logger)
        {
            _logger = logger;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            _logger.TestInputs(AbbotJsonFormat.Default.Serialize(context.Inputs));
            return new StepResult(StepOutcome.Succeeded)
            {
                Outputs = context.Inputs,
            };
        }
    }
}

public static partial class SystemEditorTestActionLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Serialized test inputs: {InputsJson}")]
    public static partial void TestInputs(this ILogger<SystemEditorTestAction.Executor> logger, string inputsJson);
}
