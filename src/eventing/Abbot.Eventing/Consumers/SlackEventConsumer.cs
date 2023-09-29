using MassTransit;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Eventing.Infrastructure;
using Serious.Abbot.Eventing.Messages;
using Serious.Slack.BotFramework;

namespace Serious.Abbot.Eventing.Consumers;

public class SlackEventConsumer : IConsumer<SlackEventReceived>
{
    readonly IBotFrameworkAdapter _slackAdapter;
    readonly IBot _bot;
    readonly ILogger<SlackEventConsumer> _logger;

    public SlackEventConsumer(IBotFrameworkAdapter slackAdapter, IBot bot, ILogger<SlackEventConsumer> logger)
    {
        _slackAdapter = slackAdapter;
        _bot = bot;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SlackEventReceived> context)
    {
        await _slackAdapter.ProcessEventAsync(
            context.Message.Envelope,
            _bot,
            context.Message.IntegrationId,

            // We are expecting that the cancellation token here is only cancelled by a disgraceful shutdown.
            // On graceful shutdown, we want to wait for the message to finish processing.
            context.CancellationToken);
    }
}
