using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents the collection of Request Headers as defined in RFC 2616.
/// </summary>
public interface IRequestHeaders : IHttpCollection
{
    /// <summary>
    ///     Gets the value with the specified key.
    /// </summary>
    /// <param name="key">
    ///     The key of the value to get.
    /// </param>
    /// <returns>
    ///     The element with the specified key, or <c>StringValues.Empty</c> if the key is not present.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    ///     key is null.
    /// </exception>
    /// <remarks>
    ///     <see cref="IHttpCollection" /> has a different indexer contract than
    ///     <see cref="IDictionary{TKey,TValue}" />, as it will return <c>StringValues.Empty</c> for missing entries
    ///     rather than throwing an Exception.
    /// </remarks>
    StringValues this[string key] { get; }

    /// <summary>
    /// Gets the value of the <code>Accept</code> request HTTP header. This header advertises
    /// which content types, expressed as MIME types, the client is able to understand.
    /// </summary>
    StringValues Accept { get; }

    /// <summary>
    /// Gets the <code>User-Agent</code> header. This is a characteristic string that allows the network
    /// protocol peers to identify the application type, operating system, software vendor or software version
    /// of the requesting software user agent.
    /// </summary>
    StringValues UserAgent { get; }

    /// <summary>
    /// Gets or sets the value of the <code>Referer</code> header. The address of the previous web page from
    /// which a link to the currently requested page was followed. This is not required to be set by a client.
    /// </summary>
    StringValues Referrer { get; }

    /// <summary>
    /// The Origin request header indicates where a request originates from. It doesn't include any path
    /// information, just the server name
    /// </summary>
    StringValues Origin { get; }

    /// <summary>
    /// Sent to request permission to deliver a webhook from the specified origin. This is part of the
    /// HTTP 1.1 Web Hooks for Event Delivery - Version 1.0.1 specification:
    /// https://github.com/cloudevents/spec/blob/v1.0.1/http-webhook.md
    /// </summary>
    StringValues WebHookRequestOrigin { get; }

    /// <summary>
    /// The WebHook-Request-Callback header is OPTIONAL and augments the WebHook-Request-Origin header. It
    /// allows the delivery target to grant send permission asynchronously, via a simple HTTPS callback.
    /// </summary>
    StringValues WebHookRequestCallback { get; }

    /// <summary>
    /// Sent to request permission to deliver a webhook at the specified rate. The rate is an integer indicating
    /// the number of requests per minute. This is part of the
    /// HTTP 1.1 Web Hooks for Event Delivery - Version 1.0.1 specification:
    /// https://github.com/cloudevents/spec/blob/v1.0.1/http-webhook.md
    /// </summary>
    int? WebHookRequestRate { get; }
}
