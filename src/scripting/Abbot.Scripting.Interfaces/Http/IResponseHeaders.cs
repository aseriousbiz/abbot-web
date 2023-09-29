using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents the collection of Response Headers as defined in RFC 2616.
/// </summary>
public interface IResponseHeaders : IHttpCollection
{
    /// <summary>
    /// Indicates this trigger will allow WebHook requests from the specified
    /// origin in response to a validation request. This is part of the
    /// HTTP 1.1 Web Hooks for Event Delivery - Version 1.0.1 specification:
    /// https://github.com/cloudevents/spec/blob/v1.0.1/http-webhook.md#42-validation-response
    /// </summary>
    string? WebHookAllowedOrigin { get; set; }

    /// <summary>
    /// Grants permission to send notifications at the specified rate. Abbot limits this value to 120.
    /// </summary>
    int WebHookAllowedRate { get; set; }

    /// <summary>
    ///     Gets or sets the value with the specified key.
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
    StringValues this[string key] { get; set; }
}
