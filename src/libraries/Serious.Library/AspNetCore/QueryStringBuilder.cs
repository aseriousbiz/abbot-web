using System.Collections;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Serious.AspNetCore;

public class QueryStringBuilder : IEnumerable<KeyValuePair<string, string>> // We just implement this so we can use a collection initializer
{
    readonly List<KeyValuePair<string, string>> _values = new();

    public void Add(string name, string? value)
    {
        if (value is not null)
        {
            _values.Add(new KeyValuePair<string, string>(name, value));
        }
    }

    public override string ToString()
    {
        return QueryString.Create(_values).ToString();
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return _values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
