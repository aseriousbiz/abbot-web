using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents Http collections such as query string, form, and headers.
/// </summary>
public interface IHttpCollection : IReadOnlyCollection<KeyValuePair<string, StringValues>>
{
    /// <summary>
    ///     Determines whether the <see cref="IHttpCollection" /> contains an element
    ///     with the specified key.
    /// </summary>
    /// <param name="key">
    /// The key to locate in the <see cref="IHttpCollection" />.
    /// </param>
    /// <returns>
    ///     true if the <see cref="IHttpCollection" /> contains an element with
    ///     the key; otherwise, false.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    ///     key is null.
    /// </exception>
    bool ContainsKey(string key);

    /// <summary>
    ///    Gets the value associated with the specified key.
    /// </summary>
    /// <param name="key">
    ///     The key of the value to get.
    /// </param>
    /// <param name="value">
    ///     The key of the value to get.
    ///     When this method returns, the value associated with the specified key, if the
    ///     key is found; otherwise, the default value for the type of the value parameter.
    ///     This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    ///    true if the object that implements <see cref="IHttpCollection" /> contains
    ///     an element with the specified key; otherwise, false.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">
    ///     key is null.
    /// </exception>
    bool TryGetValue(string key, [MaybeNullWhen(false)] out StringValues value);

    /// <summary>
    ///     Gets an <see cref="ICollection{T}" /> containing the keys of the
    ///     <see cref="IHttpCollection" />.
    /// </summary>
    /// <returns>
    ///     An <see cref="ICollection{T}" /> containing the keys of the object
    ///     that implements <see cref="IHttpCollection" />.
    /// </returns>
    ICollection<string> Keys { get; }

    /// <summary>
    ///     Gets an <see cref="ICollection{T}" /> containing the values of the
    ///     <see cref="IHttpCollection" />.
    /// </summary>
    /// <returns>
    ///     An <see cref="ICollection{T}" /> containing the values of the object
    ///     that implements <see cref="IHttpCollection" />.
    /// </returns>
    ICollection<StringValues> Values { get; }
}
