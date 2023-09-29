using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Serious.Slack.Events;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.Slack.BotFramework;

/// <summary>
/// Interface to express the relationship between an HttpContext and a Bot Builder Adapter.
/// This interface can be used for Dependency Injection.
/// </summary>
/// <remarks>
/// This is our much better alternative to <see cref="IBotFrameworkHttpAdapter"/>.
/// </remarks>
public interface IBotFrameworkAdapter
{
    /// <summary>
    /// This method can be called from inside a POST method on any Web Host that has an HttpContext implementation.
    /// Use this when we've already read the request body and content type.
    /// </summary>
    /// <param name="requestBody">The body of the request.</param>
    /// <param name="requestContentType">The content type of the request.</param>
    /// <param name="bot">The bot implementation.</param>
    /// <param name="integrationId">Identifies a custom instance of Abbot, if present.</param>
    /// <param name="retryNumber">The retry number.</param>
    /// <param name="retryReason">The retry reason.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the work queued to execute.</returns>
    Task<IActionResult> ProcessAsync(
        string requestBody,
        string requestContentType,
        IBot bot,
        int? integrationId,
        int retryNumber,
        string? retryReason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the Bot Framework pipeline on an event.
    /// </summary>
    /// <param name="eventEnvelope">The <see cref="IEventEnvelope{EventBody}"/> to process.</param>
    /// <param name="bot">The bot to run the pipeline on.</param>
    /// <param name="integrationId">Identifies a custom instance of Abbot, if present.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects
    /// or threads to receive notice of cancellation.</param>
    Task ProcessEventAsync(
        IEventEnvelope<EventBody> eventEnvelope,
        IBot bot,
        int? integrationId,
        CancellationToken cancellationToken);
}
