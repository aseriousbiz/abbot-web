using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Middleware;

namespace Serious.Abbot.BotFramework;

// BotFrameworkHttpAdapter has been deprecated in favor of CloudAdapter. But I haven't seen
// guidance on how to migrate and it's just not a priority for us.
#pragma warning disable CS0618
public class BotFrameworkAdapterWithErrorHandler : BotFrameworkHttpAdapter
#pragma warning restore CS0618
{
    public BotFrameworkAdapterWithErrorHandler(
        MessageFormatMiddleware messageFormatMiddleware,
        DiagnosticMiddleware diagnosticMiddleware,
        DebugMiddleware debugMiddleware,
        IConfiguration configuration,
        ILogger<BotFrameworkAdapterWithErrorHandler> logger)
        : base(configuration, logger)
    {
        OnTurnError = async (turnContext, exception) => {
            await turnContext.HandleUnhandledException(exception, configuration, logger);
        };

        // Register middleware.
        Use(diagnosticMiddleware);
        Use(messageFormatMiddleware);
        Use(debugMiddleware);
    }
}
