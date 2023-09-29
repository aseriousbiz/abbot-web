using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Serious.AspNetCore;

namespace Serious.Abbot.Messages;

/// <summary>
/// Represents an incoming HTTP Request that triggers a skill.
/// </summary>
public class HttpTriggerRequest
{
    public static async Task<HttpTriggerRequest> CreateAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();

        bool isForm = request.IsFormContentType();
        return new HttpTriggerRequest
        {
            Url = request.GetDisplayUrl(),
            IsJson = request.IsJsonContentType(),
            IsForm = isForm,
            HttpMethod = request.Method.ToUpperInvariant(),
            RawBody = body,
            ContentType = request.ContentType,
            Form = isForm
                ? request.Form.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value)
                : new Dictionary<string, string[]>(),
            Headers = request.Headers.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value),
            Query = request.Query.ToDictionary(kvp => kvp.Key, kvp => (string[])kvp.Value),
        };
    }

    /// <summary>
    /// The URL of the the incoming request. This is the URL of the trigger.
    /// </summary>
    public string Url { get; init; } = null!;

    /// <summary>
    /// The Raw Body of the incoming request.
    /// </summary>
    public string? RawBody { get; init; }

    /// <summary>
    /// The HTTP Method of the request.
    /// </summary>
    public string HttpMethod { get; init; } = null!;

    /// <summary>
    /// The Content Type of the request.
    /// </summary>

    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// The incoming request headers.
    /// </summary>
    public Dictionary<string, string[]> Headers { get; init; } = new();

    /// <summary>
    /// The incoming request form.
    /// </summary>
    public Dictionary<string, string[]> Form { get; init; } = new();

    /// <summary>
    /// The query string parameters as a collection.
    /// </summary>
    public Dictionary<string, string[]> Query { get; init; } = new();

    /// <summary>
    /// If true, this is a Json request
    /// </summary>
    public bool IsJson { get; set; }

    /// <summary>
    /// If true, this request has a form body
    /// </summary>
    public bool IsForm { get; set; }
}
