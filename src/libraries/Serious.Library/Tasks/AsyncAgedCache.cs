// CREDIT: https://github.com/auth0/auth0.net/blob/master/src/Auth0.AuthenticationApi/Tokens/AsyncAgedCache.cs
// Copyright (c) 2016-2020 Auth0, Inc. <support@auth0.com> (https://auth0.com)
// MIT License: https://github.com/auth0/auth0.net/blob/master/LICENSE

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Serious.Tasks;

public class AsyncAgedCache<TKey, TValue> where TKey : notnull
{
    struct Entry
    {
        public Task<TValue> Task;
        public DateTime CachedAt;
    }

    readonly ConcurrentDictionary<TKey, Entry> _cache = new ConcurrentDictionary<TKey, Entry>();
    readonly Func<TKey, Task<TValue>> _valueFactory;

    public AsyncAgedCache(Func<TKey, Task<TValue>> valueFactory)
    {
        _valueFactory = valueFactory;
    }

    public Task<TValue> GetOrAddAsync(TKey key, TimeSpan maxAge)
    {
        var now = DateTime.UtcNow;
        if (_cache.TryGetValue(key, out var entry))
        {
            var cacheExpiresAt = entry.CachedAt.Add(maxAge);
            if (now < cacheExpiresAt)
                return entry.Task;
        }

        var task = _valueFactory(key);
        _cache.TryAdd(key, new Entry { Task = task, CachedAt = now });
        return task;
    }
}
