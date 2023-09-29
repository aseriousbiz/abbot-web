using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Exceptions;

namespace Serious.Abbot.Pages;

// We handle POSTs to this page in order to render error content properly.
// However, we don't take any _action_ based on the POSTs and the redirect will likely drop the Anti-Froggery token.
// So we need to explicitly ignore the Anti-Froggery token.
[IgnoreAntiforgeryToken]
public class ErrorPage : PageModel
{
    readonly IHostEnvironment _hostEnvironment;
    readonly ILogger<ErrorPage> _logger;

    public string? ExceptionMessage { get; private set; }

    public string? ExceptionStackTrace { get; private set; }

    public ErrorPage(IHostEnvironment hostEnvironment, ILogger<ErrorPage> logger)
    {
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public static Func<HttpContext, Task> RequestDelegateFactory(
        IHostEnvironment hostEnvironment, ILoggerFactory loggerFactory) =>
        async httpContext => {
            var logger = loggerFactory.CreateLogger<ErrorPage>();
            var content = PlainTextResponse(httpContext, hostEnvironment, logger);
            await httpContext.Response.WriteAsync(content);
        };

    public IActionResult OnPost()
    {
        var content = PlainTextResponse(HttpContext, _hostEnvironment, _logger);
        return Content(content);
    }

    static string PlainTextResponse(HttpContext httpContext, IHostEnvironment hostEnvironment, ILogger<ErrorPage> logger)
    {
        var exceptionContent = (hostEnvironment.IsDevelopment() || httpContext.IsStaffMode())
            ? $"\nException detail:\n{DumpExceptionToLog(httpContext, logger)}"
            : "";

        var activityId = System.Diagnostics.Activity.Current?.Id ?? string.Empty;
        return $"Oops! Something unexpected happened. Contact '{WebConstants.SupportEmail}' for help, and provide this request ID: {activityId}.{exceptionContent}";
    }

    public void OnGet()
    {
        var exception = DumpExceptionToLog(HttpContext, _logger);

        exception = exception?.InnerException ?? exception;

        if (_hostEnvironment.IsDevelopment() || HttpContext.IsStaffMode())
        {
            ExceptionMessage = exception switch
            {
                null => "Exception was null",
                FileNotFoundException _ => "File not found",
                _ => exception.Message
            };

            ExceptionStackTrace = exception?.StackTrace;
        }
    }

    static Exception? DumpExceptionToLog(HttpContext httpContext, ILogger<ErrorPage> logger)
    {
        var pathFeature =
            httpContext.Features.Get<IExceptionHandlerPathFeature>();

        if (pathFeature is null)
        {
            logger.NoExceptionHandlerPathFeature();
            return null;
        }

        var exception = pathFeature.Error;
        var requestPath = pathFeature.Path;
        var version = Program.BuildMetadata.InformationalVersion;

        if (exception is SlackEventDeserializationException deserializationException)
        {
            var envelope = deserializationException.EventEnvelope;
            // Special case for SlackEventDeserializationException. At some point, we'll do a more generic approach,
            // But I'd like to get this in quickly. - @haacked
            logger.SlackDeserializationException(deserializationException,
                envelope?.Type,
                envelope?.EventId,
                envelope?.Event?.Type,
                envelope?.TeamId,
                envelope?.Event?.Channel,
                envelope?.Event?.User,
                requestPath,
                version,
                exception.Message);
        }
        else
        {
            logger.ExceptionHandlerTriggered(
                exception,
                exception.GetType().FullName,
                requestPath,
                version,
                exception.Message);
        }

        return exception;
    }
}

static partial class ErrorPageLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "An {ExceptionType} error occurred processing a request to {OriginalRequestPath} (Version: {Version}): {ExceptionMessage}")]
    public static partial void ExceptionHandlerTriggered(
        this ILogger<ErrorPage> logger,
        Exception ex,
        string? exceptionType,
        string originalRequestPath,
        string version,
        string? exceptionMessage);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "A Slack event deserialization exception (Type: {EnvelopeType}, Id: {SlackEventId}, EventType: {EventType}, Team: {PlatformId}, Channel: {PlatformRoomId}, User: {PlatformUserId}) occurred processing a request to {OriginalRequestPath} (Version: {Version}): {ExceptionMessage}")]
    public static partial void SlackDeserializationException(
        this ILogger<ErrorPage> logger,
        Exception ex,
        string? envelopeType,
        string? slackEventId,
        string? eventType,
        string? platformId,
        string? platformRoomId,
        string? platformUserId,
        string originalRequestPath,
        string version,
        string? exceptionMessage);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Error,
        Message = "An unknown error occurred with no 'IExceptionHandlerPathFeature' available.")]
    public static partial void NoExceptionHandlerPathFeature(
        this ILogger<ErrorPage> logger);
}
