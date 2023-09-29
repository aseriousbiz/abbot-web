using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refit;
using Serious.Logging;

namespace Serious.Abbot;

/// <summary>
/// A simple low-complexity set of fault handling helpers.
/// At some point, we might want to introduce a proper fault handling library, like Polly.
/// Until then, this gives us some simple primitives we can use and find/replace with better stuff later.
/// </summary>
public static class FaultHandler
{
    static readonly ILogger<ApiException> Log = ApplicationLoggerFactory.CreateLogger<ApiException>();

    public static async Task<T> RetryOnceAsync<T>(Func<Task<T>> action)
    {
        T response;
        try
        {
            response = await action();
        }
        catch (ApiException apiException) when (apiException.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = apiException.Headers.RetryAfter?.Delta;
            if (retryAfter.HasValue)
            {
                Log.RetryingDueToTooManyRequests(apiException, retryAfter.Value);
            }
            else
            {
                Log.RetryingDueToTooManyRequestsNoRetryDelta(apiException, apiException.Headers.ToString());
            }

            await Task.Delay(retryAfter ?? TimeSpan.FromMinutes(1));

            // We'll retry once if we hit a rate limit.
            // If this throws, it'll bubble up to the caller.
            response = await action();
        }

        return response;
    }
}

static partial class FaultHandlerLoggerExtensions
{

    [LoggerMessage(EventId = 1,
        Level = LogLevel.Error,
        Message = "Received a status code of too many requests with a retry delta of {RetryDelta}.")]
    public static partial void RetryingDueToTooManyRequests(
        this ILogger<ApiException> logger,
        ApiException apiException,
        TimeSpan retryDelta);

    [LoggerMessage(EventId = 2,
        Level = LogLevel.Error,
        Message = "Received a status code of too many requests with no retry delta {Headers}.")]
    public static partial void RetryingDueToTooManyRequestsNoRetryDelta(
        this ILogger<ApiException> logger,
        ApiException apiException,
        string headers);
}
