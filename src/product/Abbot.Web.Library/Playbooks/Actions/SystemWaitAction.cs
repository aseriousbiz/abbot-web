using System.Xml;
using MassTransit;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Eventing.Messages;

namespace Serious.Abbot.Playbooks.Actions;

public class SystemWaitAction : ActionType<SystemWaitAction.Executor>
{
    public override StepType Type { get; } = new("system.wait", StepKind.Action)
    {
        Category = "system",
        Presentation = new()
        {
            Label = "Wait",
            Icon = "fa-timer",
            Description = "Pauses the Playbook for the specified duration",
        },
        Inputs =
        {
            new("duration", "Duration", PropertyType.Duration)
            {
                Description = "The duration to wait",
                Default = "P1D",
                Required = true,
            },
        },
    };

    public class Executor : IActionExecutor, ISuspendableExecutor
    {
        readonly IClock _clock;
        readonly ILogger<Executor> _logger;

        public Executor(IClock clock, ILogger<Executor> logger)
        {
            _clock = clock;
            _logger = logger;
        }

        public async Task<StepResult> ExecuteStepAsync(StepContext context)
        {
            // Check if we're being resumed
            if (context.ResumeState is not null)
            {
                // NOTE: If we change this, we have to remember that we could be being resumed with state from an old version!
                var token = Guid.Parse(context.ResumeState["resume_publish_id"].Require<string>());
                _logger.Resumed(_clock.UtcNow, token);

                // We just got resumed, let's just go ahead and return success
                return new StepResult(StepOutcome.Succeeded);
            }

            // Parsing ISO 8601 duration strings is in XmlConvert of all places.
            // This is because XSD has a 'duration' data type that uses ISO 8601.
            // https://www.w3.org/TR/xmlschema-2/#duration
            var durationStr = context.Expect<string>("duration");
            var duration = XmlConvert.ToTimeSpan(durationStr);

            // Schedule resumption of the playbook after the duration
            var wakeTime = _clock.UtcNow.Add(duration);
            var scheduled = await context.ConsumeContext.SchedulePublish(
                wakeTime,
                new ResumeSuspendedStep(context.PlaybookRun.CorrelationId, context.ActionReference));
            _logger.ScheduledResume(wakeTime, scheduled.TokenId);

            // We gotta save the scheduled message in the state so we can cancel it
            return new StepResult(StepOutcome.Suspended)
            {
                SuspendPresenter = "_SystemWaitSuspendPresenter",
                SuspendedUntil = wakeTime,
                SuspendState =
                {
                    ["resume_publish_id"] = scheduled.TokenId,
                    ["wake_time"] = wakeTime.ToString("O"),
                }
            };
        }

        public async Task DisposeSuspendedStepAsync(StepContext context)
        {
            if (context.ResumeState is not { } suspendState)
            {
                return;
            }

            // NOTE: If we change this, we have to remember that we could be being resumed with state from an old version!
            var token = Guid.Parse(suspendState["resume_publish_id"].Require().ToString()!);
            var scheduler = context.ConsumeContext.GetPayload<IMessageScheduler>();
            await scheduler.CancelScheduledPublish<ResumeSuspendedStep>(token);
            _logger.CancelledResume(token);
        }
    }
}

public static partial class SystemWaitActionLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Scheduled ResumePlaybook message for {WakeTime} with token {TokenId}")]
    public static partial void ScheduledResume(this ILogger<SystemWaitAction.Executor> logger, DateTime wakeTime,
        Guid tokenId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Resumed at {ResumeTime} by ResumePlaybook message with token {TokenId}")]
    public static partial void Resumed(this ILogger<SystemWaitAction.Executor> logger, DateTime resumeTime,
        Guid tokenId);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Information,
        Message = "Cancelled ResumePlaybook message with token {TokenId}")]
    public static partial void CancelledResume(this ILogger<SystemWaitAction.Executor> logger, Guid tokenId);
}
