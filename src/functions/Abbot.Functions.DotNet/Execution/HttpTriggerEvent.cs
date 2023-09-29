using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Messages;
using Serious.Abbot.Runtime;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions;

public class HttpTriggerEvent : IHttpTriggerEvent
{
    public static readonly HttpTriggerEvent Empty = new(
        "http://app.ab.bot/",
        "None",
        string.Empty,
        string.Empty,
        false,
        false,
        new Dictionary<string, string[]>(),
        new Dictionary<string, string[]>(),
        new Dictionary<string, string[]>());

    public HttpTriggerEvent(HttpTriggerRequest httpTriggerRequest)
        : this(
            httpTriggerRequest.Url,
            httpTriggerRequest.HttpMethod,
            httpTriggerRequest.RawBody ?? string.Empty,
            httpTriggerRequest.ContentType,
            httpTriggerRequest.IsJson,
            httpTriggerRequest.IsForm,
            httpTriggerRequest.Headers,
            httpTriggerRequest.Form,
            httpTriggerRequest.Query)
    {
    }

    HttpTriggerEvent(
        string url,
        string method,
        string body,
        string contentType,
        bool isJson,
        bool isForm,
        Dictionary<string, string[]> headers,
        Dictionary<string, string[]> form,
        Dictionary<string, string[]> query)
    {
        Url = new Uri(url);
        HttpMethod = new HttpMethod(method);
        RawBody = body;
        Headers = new RequestHeaders(headers);
        Form = new FormCollection(form);
        Query = new QueryCollection(query);
        ContentType = contentType;
        IsJson = isJson;
        IsForm = isForm;
    }

    public HttpMethod HttpMethod { get; }
    public string RawBody { get; }
    public string ContentType { get; }

    public Uri Url { get; }

    public IRequestHeaders Headers { get; }
    public IFormCollection Form { get; }
    public IQueryCollection Query { get; }
    public bool IsJson { get; }
    public bool IsForm { get; }
    public dynamic? DeserializeBody()
    {
        if (!IsJson)
        {
            throw new InvalidOperationException("This is not a JSON request. Make sure to check `Bot.Request.IsJson` first.");
        }

        return JsonConvert.DeserializeObject(RawBody);
    }

    public T? DeserializeBodyAs<T>()
    {
        return IsJson
            ? JsonConvert.DeserializeObject<T>(RawBody)
            : throw new InvalidOperationException(
                "This is not a JSON request. Make sure to check `Bot.Request.IsJson` first.");
    }

    public dynamic Body =>
        IsJson
            ? JObject.Parse(RawBody)
            : throw new InvalidOperationException(
                "The content type must be `application/json` in order to deserialize the body.");
}
