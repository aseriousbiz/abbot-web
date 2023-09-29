using System;
using System.Collections.Generic;
using System.Globalization;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Runtime;

/// <summary>
/// Represents the collection of Response Headers as defined in RFC 2616.
/// </summary>
public class ResponseHeaders : HttpCollection, IResponseHeaders
{
    readonly HashSet<string> _disallowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Referer",
        "Server",
        "User-Agent",
        "Content-Security-Policy",
        "Strict-Transport-Security",
        "Public-Key-Pins",
        "X-Frame-Options",
        "X-Xss-Protection",
        "X-Powered-By",
        "X-AspNet-Version",
        "Content-Length"
    };

    /// <summary>
    /// Indicates this trigger will allow WebHook requests from the specified
    /// origin in response to a validation request. This is part of the
    /// HTTP 1.1 Web Hooks for Event Delivery - Version 1.0.1 specification:
    /// https://github.com/cloudevents/spec/blob/v1.0.1/http-webhook.md
    /// </summary>
    public string? WebHookAllowedOrigin
    {
        get => this["WebHook-Allowed-Origin"];
        set => this["WebHook-Allowed-Origin"] = value;
    }

    public int WebHookAllowedRate
    {
        get => int.TryParse(this["WebHook-Allowed-Rate"], out var rate) ? rate : 0;
        set {
            this["WebHook-Allowed-Rate"] = value switch
            {
                <= 0 => throw new ArgumentOutOfRangeException(nameof(WebHookAllowedRate),
                    $"{WebHookAllowedRate} must be a value greater than 0."),
                > 120 => throw new ArgumentOutOfRangeException(nameof(WebHookAllowedRate),
                    $"{WebHookAllowedRate} must be 120 or less."),
                _ => value.ToString(CultureInfo.InvariantCulture)
            };
        }
    }

    /// <summary>
    /// Get or sets the associated value from the collection as a single string.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <returns>the associated value from the collection as a Strings or Strings.Empty if the key is not present.</returns>
    public StringValues this[string key]
    {
        get => TryGetValue(key, out var value)
            ? value
            : StringValues.Empty;
        set {
            if (_disallowedHeaders.Contains(key))
            {
                throw new InvalidOperationException($"Setting the {value} response header is not allowed.");
            }

            Set(key, value);
        }
    }
}
