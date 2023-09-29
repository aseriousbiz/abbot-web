namespace Serious.Abbot.Scripting;

/// <summary>
/// Contains the parsed form values.
/// </summary>
public interface IFormCollection : IHttpCollection
{
    /// <summary>
    ///     Gets the value with the specified key.
    /// </summary>
    /// <param name="key">
    ///     The key of the value to get.
    /// </param>
    /// <returns>
    ///     The element with the specified key, or <see cref="StringValues.Empty">StringValues.Empty</see> if the key is not present.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    ///     key is null.
    /// </exception>
    /// <remarks>
    ///     <see cref="IHttpCollection" /> has a different indexer contract than
    ///     <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.idictionary-2?view=net-5.0">IDictionary{TKey,TValue}</see>, as it will return <c>StringValues.Empty</c> for missing entries
    ///     rather than throwing an Exception.
    /// </remarks>
    StringValues this[string key] { get; }
}
