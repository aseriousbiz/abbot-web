using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// The HttpRequest query string collection
/// </summary>
public interface IQueryCollection : IHttpCollection
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
}
