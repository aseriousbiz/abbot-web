using System.Collections.Generic;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Runtime;

public class QueryCollection : HttpCollection, IQueryCollection
{
    public QueryCollection(Dictionary<string, string[]> headers) : base(headers)
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

}
