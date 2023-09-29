using System.Collections.Generic;

namespace Serious.Collections;

/// <summary>
/// A list that contains results grouped in pages.
/// </summary>
public interface IPaginatedList : IPartialList
{
    /// <summary>
    /// The current page number of the list.
    /// </summary>
    int PageNumber { get; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    int TotalPages { get; }

    /// <summary>
    /// The maximum number of items per page.
    /// Every page except the last page will have exactly this many items.
    /// The last page will have no more than this many items, but may have fewer items.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Whether or not there is a previous page.
    /// </summary>
    bool HasPreviousPage { get; }

    /// <summary>
    /// Whether or not there is a next page.
    /// </summary>
    bool HasNextPage { get; }

    /// <summary>
    /// The set of pages.
    /// </summary>
    IEnumerable<IPage> Pages { get; }

    /// <summary>
    /// The query string parameter name used to specify the page number for this particular list. Defaults to "p".
    /// </summary>
    string PageQueryStringParameterName { get; set; }
}

public interface IPaginatedList<out T> : IPaginatedList, IPartialList<T>
{
}
