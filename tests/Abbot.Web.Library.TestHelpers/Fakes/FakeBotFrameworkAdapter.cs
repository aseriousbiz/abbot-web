using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Serious.Slack.BotFramework;
using Serious.Slack.Events;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.TestHelpers;

public class FakeBotFrameworkAdapter : BotAdapter, IBotFrameworkAdapter, IBotFrameworkHttpAdapter
{
    public Task<IActionResult> ProcessAsync(
        string requestBody,
        string requestContentType,
        IBot bot,
        int? integrationId,
        int retryNumber,
        string? retryReason,
        CancellationToken cancellationToken = default)
    {
        ReceivedProcessedRequest = new ProcessedRequest(requestBody, requestContentType, bot);
        return Task.FromResult<IActionResult>(new ContentResult());
    }

    public ProcessedRequest ReceivedProcessedRequest { get; private set; }

    public Task ProcessEventAsync(IEventEnvelope<EventBody> eventEnvelope, IBot bot, int? integrationId, CancellationToken cancellationToken)
    {
        ReceivedProcessedEvent = new ProcessedEvent(eventEnvelope, bot);
        return Task.CompletedTask;
    }

    public ProcessedEvent ReceivedProcessedEvent { get; private set; }

    public record struct ProcessedRequest(string RequestBody, string RequestContentType, IBot Bot);
    public record struct ProcessedEvent(IEventEnvelope<EventBody> EventEnvelope, IBot Bot);

    public Task ProcessAsync(
        HttpRequest httpRequest,
        HttpResponse httpResponse,
        IBot bot,
        CancellationToken cancellationToken = new())
    {
        return Task.CompletedTask;
    }

    public override Task<ResourceResponse[]> SendActivitiesAsync(
        ITurnContext turnContext,
        Activity[] activities,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Array.Empty<ResourceResponse>());
    }

    public override Task<ResourceResponse> UpdateActivityAsync(
        ITurnContext turnContext,
        Activity activity,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new ResourceResponse());
    }

    public override Task DeleteActivityAsync(
        ITurnContext turnContext,
        ConversationReference reference,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
