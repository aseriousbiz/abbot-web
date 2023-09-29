using System.Collections.Generic;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Runtime;

public class RequestHeaders : HttpCollection, IRequestHeaders
{
    public RequestHeaders(Dictionary<string, string[]> headers)
        : base(headers)
    {
    }

    /// <summary>
    /// Get or sets the associated value from the collection as a single string.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <returns>the associated value from the collection as a Strings or Strings.Empty if the key is not present.</returns>
    public StringValues this[string key] => TryGetValue(key, out var value)
        ? value
        : StringValues.Empty;

    public StringValues Accept => this["Accept"];
    public StringValues UserAgent => this["User-Agent"];
    public StringValues Referrer => this["Referer"];
    public StringValues Origin => this["Origin"];
    public StringValues WebHookRequestOrigin => this["WebHook-Request-Origin"];
    public StringValues WebHookRequestCallback => this["WebHook-Request-Callback"];

    public int? WebHookRequestRate => int.TryParse(this["WebHook-Request-Rate"], out var rate)
        ? rate
        : null;
}
