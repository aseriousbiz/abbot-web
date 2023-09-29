using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Net.Http.Headers;

namespace Serious.AspNetCore;

public static class HttpRequestExtensions
{
    /// <summary>
    ///  Determine whether the request is on the local host.
    /// This is good enough for my needs.
    /// </summary>
    /// <param name="request"></param>
    public static bool IsLocal(this HttpRequest request)
    {
#if DEBUG
        var connection = request.HttpContext.Connection;
        var remoteIpAddress = connection.RemoteIpAddress;
        return remoteIpAddress == null
               || remoteIpAddress.ToString() == "::1"
               || remoteIpAddress.ToString() == "127.0.0.1"
               || IPAddress.IsLoopback(remoteIpAddress);
#else
            return false;
#endif
    }

    /// <summary>
    /// Get the original URL for this request. For example, when using ngrok, we want the original ngrok host, not the
    /// localhost.
    /// </summary>
    /// <param name="request">The <see cref="HttpRequest"/> request.</param>
    public static string GetOriginalUrl(this HttpRequest request)
    {
        var originalHost = request.Headers["X-Original-Host"].SingleOrDefault()
            ?? request.Headers["X-Forwarded-Host"].SingleOrDefault();

        var host = originalHost ?? request.Host.Value;
        var pathBase = request.PathBase.Value;
        var path = request.Path.Value;
        var queryString = request.QueryString.Value;

        return $"{request.Scheme}://{host}{pathBase}{path}{queryString}";
    }

    public static Uri GetFullyQualifiedUrl(this HttpRequest request, string path)
    {
        return request.GetFullyQualifiedUrl(request.Host.Host, path);
    }

    public static Uri GetFullyQualifiedUrl(this HttpRequest request, string host, string path)
    {
        return new(request.GetUrlToHost(host), path);
    }

    public static Uri GetUrlToHost(this HttpRequest request, string host)
    {
        var portedHost = request.Host.Port.HasValue
            ? $"{host}:{request.Host.Port}"
            : host;

        return new Uri($"https://{portedHost}/");
    }

    public const string XmlHttpRequest = nameof(XmlHttpRequest);

    public static bool IsTurboFrameRequest(this ViewContext viewContext, DomId id) =>
        viewContext.HttpContext.Request.IsTurboFrameRequest(id);

    public static bool IsTurboFrameRequest(this HttpRequest request, DomId id) =>
        request.Headers["turbo-frame"].Contains(id);

    public static bool IsTurboRequest(this ViewContext viewContext) => IsTurboRequest(viewContext.HttpContext.Request);
    public static bool IsTurboRequest(this HttpRequest request) =>
        request.AcceptsMediaType("text/vnd.turbo-stream.html");

    public static bool IsAjaxRequest(this ViewContext viewContext) => IsAjaxRequest(viewContext.HttpContext.Request);

    public static bool IsAjaxRequest(this HttpRequest request)
    {
        return request.Headers["X-Requested-With"] == XmlHttpRequest
            || request.IsTurboRequest();
    }

    public static bool AcceptsJson(this HttpRequest request)
    {
        return request.AcceptsMediaType("application/json");
    }

    public static bool AcceptsXml(this HttpRequest request)
    {
        return request.AcceptsMediaType("application/xml", "text/xml");
    }

    static bool AcceptsMediaType(this HttpRequest request, params string[] mediaTypes)
    {
        var mediaTypeValues = mediaTypes
            .Select(t => new MediaTypeHeaderValue(t))
            .ToList();
        var accept = request.GetTypedHeaders().Accept;
        return accept is not null && accept.Any(a => mediaTypeValues.Any(v => v.IsSubsetOf(a)));
    }

    /// <summary>
    /// If the client making the request accepts the content type of the request, return that content type,
    /// otherwise return null.
    /// </summary>
    /// <param name="request">The incoming HTTP request</param>
    /// <param name="contentType">The retrieved content type.</param>
    /// <returns></returns>
    public static bool TryGetAcceptedContentType(
        this HttpRequest request,
        [NotNullWhen(true)] out MediaTypeHeaderValue? contentType)
    {
        var headers = request.GetTypedHeaders();
        var accepts = headers.Accept;
        var mediaType = headers.ContentType;
        if (mediaType is null)
        {
            contentType = null;
            return false;
        }
        var accepted = accepts.Any(accept => mediaType.IsSubsetOf(accept));
        contentType = accepted ? mediaType : null;
        return accepted;
    }

    public static bool IsJsonContentType(this HttpRequest request)
    {
        return request.IsContentType("application/json", "text/json");
    }

    public static bool IsXmlContentType(this HttpRequest request)
    {
        return request.IsContentType("application/xml", "text/xml");
    }

    public static bool IsFormContentType(this HttpRequest request)
    {
        return request.IsContentType(
            "application/x-www-form-urlencoded",
            "multipart/form-data");
    }

    static bool IsContentType(this HttpRequest request, params string[] compare)
    {
        var header = request.GetTypedHeaders().ContentType;
        return header is not null
               && compare.Any(c => header.IsSubsetOf(new MediaTypeHeaderValue(c)));
    }

    // * CREDIT: adapted from https://stackoverflow.com/questions/42000362/creating-a-proxy-to-another-web-api-with-asp-net-core/62339908#62339908 with minor style changes.
    public static HttpRequestMessage CreateProxyHttpRequest(this HttpRequest request, Uri uri)
    {
        var requestMessage = new HttpRequestMessage();
        var requestMethod = request.Method;
        if (!HttpMethods.IsGet(requestMethod) &&
            !HttpMethods.IsHead(requestMethod) &&
            !HttpMethods.IsDelete(requestMethod) &&
            !HttpMethods.IsTrace(requestMethod))
        {
            var streamContent = new StreamContent(request.Body);
            requestMessage.Content = streamContent;
        }

        foreach (var (key, value) in request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(key, value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(key, value.ToArray());
            }
        }

        requestMessage.Headers.Host = uri.Authority;
        requestMessage.RequestUri = uri;
        requestMessage.Method = new HttpMethod(request.Method);

        return requestMessage;
    }
}
