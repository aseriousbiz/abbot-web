using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.Http;

public class RequestLoggingHttpMessageHandler : DelegatingHandler
{
    readonly ILogger<RequestLoggingHttpMessageHandler> _logger;
    readonly ISensitiveLogDataProtector _dataProtector;

    public RequestLoggingHttpMessageHandler(
        ILogger<RequestLoggingHttpMessageHandler> logger,
        ISensitiveLogDataProtector dataProtector)
    {
        _logger = logger;
        _dataProtector = dataProtector;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestBody = await request.Content.SafelyReadAsStringAsync(request.Headers, cancellationToken: cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.SafelyReadAsStringAsync(response.Headers, cancellationToken: cancellationToken);

            _logger.LogUnsuccessfulRequestResponse(
                request.Method,
                request.RequestUri,
                _dataProtector.Protect(requestBody),
                (int)response.StatusCode,
                response.ReasonPhrase,
                string.Join("\n",
                    response.Headers
                        .Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")),
                _dataProtector.Protect(responseBody));
        }

        return response;
    }
}

static partial class RequestLoggingHttpMessageHandlerLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message =
            """
            HTTP Error!

            Request: {RequestMethod} {RequestUri}
            {ProtectedRequestBody}

            Response: {ResponseStatusCode} {ResponseReason}
            {ResponseHeaders}

            {ProtectedResponseBody}

            """
        )]
    public static partial void LogUnsuccessfulRequestResponse(
        this ILogger<RequestLoggingHttpMessageHandler> logger,
        HttpMethod requestMethod,
        Uri? requestUri,
        string? protectedRequestBody,
        int responseStatusCode,
        string? responseReason,
        string responseHeaders,
        string? protectedResponseBody
    );
}
