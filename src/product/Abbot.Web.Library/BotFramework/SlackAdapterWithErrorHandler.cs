using Microsoft.Bot.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Middleware;
using Serious.Logging;
using Serious.Slack;
using Serious.Slack.BotFramework;

namespace Serious.Abbot.BotFramework;

/// <summary>
/// The handler for requests from Slack.
/// </summary>
public class SlackAdapterWithErrorHandler : SlackAdapter
{
    readonly IConfiguration _configuration;

    public SlackAdapterWithErrorHandler(
        IOptionsMonitor<SlackEventOptions> slackEventOptions,
        SlackEventDeduplicator deduplicator,
        ISlackApiClient slackClient,
        IEventQueueClient eventQueueClient,
        MessageFormatMiddleware messageFormatMiddleware,
        DiagnosticMiddleware diagnosticMiddleware,
        DebugMiddleware debugMiddleware,
        IConfiguration configuration,
        ISensitiveLogDataProtector dataProtector,
        ILogger<SlackAdapterWithErrorHandler> logger)
        : base(slackEventOptions, deduplicator, slackClient, eventQueueClient, dataProtector, logger)
    {
        _configuration = configuration;
        // Register middleware.
        Use(diagnosticMiddleware);
        Use(messageFormatMiddleware);
        Use(debugMiddleware);
    }

    protected override async Task HandleUnhandledExceptionAsync(ITurnContext turnContext, Exception exception)
    {
        await turnContext.HandleUnhandledException(exception, _configuration, Logger);
    }
}
