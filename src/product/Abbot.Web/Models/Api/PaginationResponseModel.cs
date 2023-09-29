using Serious.Collections;

namespace Serious.Abbot.Models.Api;

/// <summary>
///
/// </summary>
/// <param name="PageNumber">The current page number of the list.</param>
/// <param name="TotalPages">The total number of pages.</param>
/// <param name="PageSize">
/// The maximum number of items per page.
/// Every page except the last page will have exactly this many items.
/// The last page will have no more than this many items, but may have fewer items.
/// </param>
/// <param name="HasPreviousPage">Whether or not there is a previous page.</param>
/// <param name="HasNextPage">Whether or not there is a next page.</param>
public record PaginationResponseModel(
    int PageNumber,
    int TotalPages,
    int PageSize,
    bool HasPreviousPage,
    bool HasNextPage)
{
    public static PaginationResponseModel Create<T>(IPaginatedList<T> list) =>
        new(
            list.PageNumber,
            list.TotalPages,
            list.PageSize,
            list.HasPreviousPage,
            list.HasNextPage);
}
