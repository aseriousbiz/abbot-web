// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Modified by A Serious Business, Inc.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Runtime;

/// <summary>
/// Represents a wrapper for RequestHeaders and ResponseHeaders.
/// </summary>
public abstract class HttpCollection : IHttpCollection
{
#pragma warning disable CA1805
    static readonly Enumerator EmptyEnumerator = new();
#pragma warning restore
    // Pre-box
    static readonly IEnumerator<KeyValuePair<string, StringValues>> EmptyIEnumeratorType = EmptyEnumerator;
    static readonly IEnumerator EmptyIEnumerator = EmptyEnumerator;

    readonly Dictionary<string, StringValues> _store;

    protected HttpCollection()
        : this(new Dictionary<string, StringValues>())
    {
    }

    protected HttpCollection(Dictionary<string, string[]> headers)
        : this(headers.ToDictionary(kvp => kvp.Key, kvp => new StringValues(kvp.Value), StringComparer.OrdinalIgnoreCase))
    {
    }

    HttpCollection(Dictionary<string, StringValues> store)
    {
        _store = store;
    }

    protected void Set(string key, StringValues value)
    {
        _store[key] = value;
    }

    /// <summary>
    /// Gets the number of elements contained in the <see cref="HttpCollection" />;.
    /// </summary>
    /// <returns>The number of elements contained in the <see cref="HttpCollection" />.</returns>
    public int Count => _store.Count;

    public ICollection<string> Keys => _store.Keys;

    public ICollection<StringValues> Values => _store.Values;

    /// <summary>
    /// Returns a value indicating whether the specified object occurs within this collection.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>true if the specified object occurs within this collection; otherwise, false.</returns>
    public bool Contains(KeyValuePair<string, StringValues> item)
    {
        var (key, stringValues) = item;
        return _store.TryGetValue(key, out var value)
               && StringValues.Equals(value, stringValues);
    }

    /// <summary>
    /// Determines whether the <see cref="HttpCollection" /> contains a specific key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>true if the <see cref="HttpCollection" /> contains a specific key; otherwise, false.</returns>
    public bool ContainsKey(string key) => _store.ContainsKey(key);

    /// <summary>
    /// Retrieves a value from the dictionary.
    /// </summary>
    /// <param name="key">The header name.</param>
    /// <param name="value">The value.</param>
    /// <returns>true if the <see cref="HttpCollection" /> contains the key; otherwise, false.</returns>
    public bool TryGetValue(string key, out StringValues value)
    {
        return _store.TryGetValue(key, out value);
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="Enumerator" /> object that can be used to iterate through the collection.</returns>
    public Enumerator GetEnumerator()
    {
        return _store.Count == 0
            ? EmptyEnumerator
            : new Enumerator(_store.GetEnumerator());
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="IEnumerator" /> object that can be used to iterate through the collection.</returns>
    IEnumerator<KeyValuePair<string, StringValues>> IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator()
    {
        return _store.Count == 0
            ? EmptyIEnumeratorType
            : _store.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through a collection.
    /// </summary>
    /// <returns>An <see cref="IEnumerator" /> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return _store.Count == 0
            ? EmptyIEnumerator
            : _store.GetEnumerator();
    }

    public struct Enumerator : IEnumerator<KeyValuePair<string, StringValues>>
    {
        // Do NOT make this readonly, or MoveNext will not work
        Dictionary<string, StringValues>.Enumerator _dictionaryEnumerator;
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        bool _notEmpty;

        internal Enumerator(Dictionary<string, StringValues>.Enumerator dictionaryEnumerator)
        {
            _dictionaryEnumerator = dictionaryEnumerator;
            _notEmpty = true;
        }

        public bool MoveNext()
        {
            return _notEmpty && _dictionaryEnumerator.MoveNext();
        }

        public KeyValuePair<string, StringValues> Current => _notEmpty
            ? _dictionaryEnumerator.Current
            : default;

        public void Dispose()
        {
        }

        object IEnumerator.Current => Current;

        void IEnumerator.Reset()
        {
            if (_notEmpty)
            {
                ((IEnumerator)_dictionaryEnumerator).Reset();
            }
        }
    }
}
