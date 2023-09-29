using System;
using System.Collections.Generic;
using System.Linq;

namespace Serious.TestHelpers
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Attempts to get the value of the specified key. If the key is not found, calls the
        /// <paramref name="creator"/> method to create a value and stores it associated with the key.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="creator">Method to return a value if the key is not in the dictionary.</param>
        public static TValue GetOrCreate<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, TKey key,
            Func<TKey, TValue> creator)
        {
            if (!dictionary.TryGetValue(key, out var value))
            {
                value = creator(key);
                dictionary.Add(key, value);
            }

            return value;
        }

        /// <summary>
        /// Converts a dictionary into a list of key-value pairs, in a stable order.
        /// The order of dictionary pairs is _not guaranteed_ to be stable, hence we order by key.
        /// This depends on <typeparamref name="TKey"/> having a comparer.
        /// </summary>
        /// <param name="dict">The dictionary.</param>
        public static (TKey, TValue)[] ToOrderedPairs<TKey, TValue>(this IDictionary<TKey, TValue> dict) =>
            dict.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToArray();

        /// <summary>
        /// Sets <paramref name="key"/> with <paramref name="value"/> in <paramref name="dict"/>.
        /// </summary>
        /// <typeparam name="TKey">The key type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <typeparam name="TDict">The dictionary type.</typeparam>
        /// <param name="dict">The dictionary.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns><paramref name="dict"/></returns>
        public static TDict With<TKey, TValue, TDict>(this TDict dict, TKey key, TValue value)
            where TDict : IDictionary<TKey, TValue>
        {
            dict[key] = value;
            return dict;
        }
    }
}
