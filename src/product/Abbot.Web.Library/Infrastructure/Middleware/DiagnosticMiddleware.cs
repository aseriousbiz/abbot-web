using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Serious.Abbot.BotFramework;
using Serious.Logging;
using Activity = Microsoft.Bot.Schema.Activity;

namespace Serious.Abbot.Infrastructure.Middleware;

public class DiagnosticMiddleware : IMiddleware
{
    static readonly ILogger<DiagnosticMiddleware> Log = ApplicationLoggerFactory.CreateLogger<DiagnosticMiddleware>();
    const string TimingMiddlewareFlag = nameof(TimingMiddlewareFlag);
    static readonly IReadOnlyCollection<string> TimingSuffixes = new[] { " --timing", " —timing" }.ToReadOnlyList();

    public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next,
        CancellationToken cancellationToken = new CancellationToken())
    {
        using var scope = Log.BeginProcessingTurn(
            turnContext.Activity.Id,
            turnContext.Activity.Type);
        Stopwatch? stopwatch = null;
        if (ContainsTimingFlag(turnContext))
        {
            stopwatch = new Stopwatch();
            stopwatch.Start();
            turnContext.TurnState.Add(TimingMiddlewareFlag, "true");

            // Strip off the timing suffix so skills don't see it.
            turnContext.Activity.Text = TimingSuffixes
                .Aggregate(
                    turnContext.Activity.Text,
                    (current, suffix) => current.TrimSuffix(suffix, StringComparison.Ordinal));
        }

        turnContext.OnSendActivities(async (_, activities, nextSend) => {
            try
            {
                if (ContainsTimingFlag(turnContext) && stopwatch is not null)
                {
                    stopwatch.Stop();
                    var elapsed = stopwatch.Elapsed;
                    foreach (var activity in activities)
                    {
                        AppendTimingToActivityMessage(activity, elapsed);
                    }
                }

                return await nextSend().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Logging here mean we get more context than if we log at the top-level exception handler.
                Log.UnhandledExceptionSendingActivity(ex, ex.GetType().FullName, ex.Message);
                throw;
            }
        });

        // process bot logic
        await next(cancellationToken).ConfigureAwait(false);
    }

    static bool ContainsTimingFlag(ITurnContext turnContext)
    {
        if (turnContext.TurnState.ContainsKey(TimingMiddlewareFlag))
        {
            return true;
        }

        if (!turnContext.IsMessage())
        {
            return false;
        }

        var activity = turnContext.Activity;

        var text = activity.GetReplyMessageText();
        var attachments = activity.Attachments ?? Array.Empty<Attachment>();

        return TimingSuffixes.Any(suffix => text.EndsWith(suffix, StringComparison.Ordinal)
                                            || attachments.Any(file =>
                                                file.Name is not null
                                                && file.Name.EndsWith(suffix, StringComparison.Ordinal)));
    }

    static void AppendTimingToActivityMessage(Activity activity, TimeSpan elapsed)
    {
        var elapsedMs = (int)Math.Round(elapsed.TotalMilliseconds);
        var timingInfo = $" _(Took {elapsedMs.ToString(CultureInfo.InvariantCulture)}ms)_";

        activity.AppendToMessage(timingInfo);
    }
}

static partial class DiagnosticMiddlewareLoggerExtensions
{
    static readonly Func<ILogger, string, string, IDisposable?> ProcessingTurnScope =
        LoggerMessage.DefineScope<string, string>(
            "Processing Turn. ActivityId: {TurnActivityId}, ActivityType: {TurnActivityType}");

    public static IDisposable? BeginProcessingTurn(this ILogger<DiagnosticMiddleware> logger, string activityId, string activityType) =>
        ProcessingTurnScope(logger, activityId, activityType);

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Unhandled {ExceptionType} exception while sending activity: {ExceptionMessage}")]
    public static partial void UnhandledExceptionSendingActivity(this ILogger<DiagnosticMiddleware> logger, Exception exception, string? exceptionType, string exceptionMessage);
}
