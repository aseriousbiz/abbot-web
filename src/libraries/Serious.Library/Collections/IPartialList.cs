using System.Collections.Generic;

namespace Serious.Collections;

/// <summary>
/// Represents a subset of a query result. For example, if returning a paged list, this might be the result
/// of one page. Or if taking a random sample of a list, this might be the sample, but contains information
/// about the total list.
/// </summary>
public interface IPartialList
{
    /// <summary>
    /// The total number of records in the result set.
    /// </summary>
    int TotalCount { get; }
}

/// <summary>
/// Represents a subset of a query result. For example, if returning a paged list, this might be the result
/// of one page. Or if taking a random sample of a list, this might be the sample, but contains information
/// about the total list.
/// </summary>
public interface IPartialList<out T> : IReadOnlyList<T>, IPartialList
{
}
