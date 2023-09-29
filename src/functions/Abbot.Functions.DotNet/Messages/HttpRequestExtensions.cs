using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Serious.Abbot.Messages;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Functions.Messages;

public static class HttpRequestDataExtensions
{
    /// <summary>
    /// Deserialize the incoming request into a <see cref="SkillMessage" />.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    public static async Task<SkillMessage> ReadAsSkillMessageAsync(this HttpRequestData request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        using var reader = new StreamReader(request.Body);
        string requestBodyText = await reader.ReadToEndAsync();
        var message = AbbotJsonFormat.Default.Deserialize<SkillMessage>(requestBodyText)
                      ?? throw new InvalidOperationException($"Could not read payload as SkillMessage\n\n{requestBodyText}");

        return message;
    }

    public static async Task<HttpResponseData> CreatePlainTextResponseAsync(this HttpRequestData request, string content)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain");
        await response.WriteStringAsync(content);
        return response;
    }

    /// <summary>
    /// Retrieves the traceparent header from the incoming request. This is part of the W3C Trace Context spec.
    /// </summary>
    /// <remarks>
    /// Describes the position of the incoming request in its trace graph in a portable, fixed-length format.
    /// </remarks>
    /// <param name="request">The incoming request.</param>
    /// <returns>The first value of the traceparent header in the headers collection, otherwise <c>null</c>.</returns>
    public static string? GetTraceParent(this HttpRequestData request)
    {
        return request.GetSingleHeaderValue("traceparent");
    }

    /// <summary>
    /// Retrieves the tracestate header from the incoming request. This is part of the W3C Trace Context spec.
    /// </summary>
    /// <remarks>
    /// Extends traceparent with vendor-specific data represented by a set of name/value pairs.
    /// </remarks>
    /// <param name="request">The incoming request.</param>
    /// <returns>The first value of the tracestate header in the headers collection, otherwise <c>null</c>.</returns>
    public static string? GetTraceState(this HttpRequestData request)
    {
        return request.GetSingleHeaderValue("tracestate");
    }

    /// <summary>
    /// Retrieves a single header value for the specified header <param name="name" />. If there is more than
    /// one value, or if there are no values, then it returns null.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="name">The header name.</param>
    /// <returns>The header value or null if there are more than one value or no values.</returns>
    public static string? GetSingleHeaderValue(this HttpRequestData request, string name)
    {
        return request.Headers.TryGetValues(name, out var values)
            ? values.FirstOrDefault()
            : null;
    }
}
