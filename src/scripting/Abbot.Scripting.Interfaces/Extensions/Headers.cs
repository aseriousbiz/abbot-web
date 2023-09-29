using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

namespace Serious.Abbot.Scripting;

/// <summary>
/// A collection of HTTP headers
/// </summary>
[SuppressMessage("Microsoft.Naming", "CA1724", Justification = "This is part of a replacement for System.Net.Http.Headers for skill authors.")]
public class Headers : IEnumerable<KeyValuePair<string, string>>
{
    readonly IDictionary<string, string> _headers;

    /// <summary>
    /// Constructs an instance of <see cref="Headers"/>
    /// </summary>
    public Headers() : this(new Dictionary<string, string>())
    {
    }

    /// <summary>
    /// Constructs an instance of <see cref="Headers"/>
    /// </summary>
    /// <param name="headers">A dictionary of initial header values</param>
    public Headers(IDictionary<string, string> headers)
    {
        _headers = headers;
    }

    /// <summary>
    /// Adds a header to the collection.
    /// </summary>
    /// <param name="key">Header name</param>
    /// <param name="value">Header value</param>
    public void Add(string key, string value)
    {
        _headers.Add(key, value);
    }

    /// <summary>
    /// Retrieves the header with the specified key.
    /// </summary>
    /// <param name="key">The header name</param>
    public string this[string key]
    {
        get => _headers[key];
        set => _headers[key] = value;
    }

    /// <summary>
    /// Copies the headers into a <see href="https://docs.microsoft.com/en-us/dotnet/api/system.net.http.headers.httprequestheaders?view=net-5.0">HttpRequestHeaders</see> collection.
    /// </summary>
    /// <param name="headers"></param>
    public void CopyTo(HttpRequestHeaders headers)
    {
        foreach (var (key, value) in _headers)
        {
            headers.Add(key, value);
        }
    }

    /// <summary>
    /// Gets the enumerator for this collection.
    /// </summary>
    /// <returns>The enumerator</returns>
    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _headers.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _headers.GetEnumerator();
    }
}
