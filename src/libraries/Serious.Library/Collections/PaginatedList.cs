using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Serious.Collections;

public static class PaginatedList
{
    public static IPaginatedList<T> Empty<T>() => new PaginatedList<T>(Enumerable.Empty<T>(), 0, 0, 0);

    /// <summary>
    /// Creates a paginated list using the provided queryable.
    /// </summary>
    /// <param name="source">The source queryable. It should not have a skip and take applied.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The number of elements on a page.</param>
    /// <param name="pageQueryStringParameterName">The query string parameter name used to page this pager (defaults to "p")</param>
    /// <typeparam name="T">The type of entity to paginate</typeparam>
    public static Task<IPaginatedList<T>> CreateAsync<T>(IQueryable<T> source, int pageNumber, int pageSize, string pageQueryStringParameterName = "p")
    {
        return CreateAsync(source, pageNumber, pageSize, e => e, pageQueryStringParameterName);
    }

    /// <summary>
    /// Creates a paginated list using the provided queryable, but converts each entity of the page to the
    /// specified model.
    /// </summary>
    /// <param name="source">The source queryable. It should not have a skip and take applied.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The number of elements on a page.</param>
    /// <param name="transform">A function to turn the entity into the model</param>
    /// <param name="pageQueryStringParameterName">The query string parameter name used to page this pager (defaults to "p")</param>
    /// <typeparam name="TEntity">The type of entity for the queryable</typeparam>
    /// <typeparam name="TModel">The type of model for the list result</typeparam>
    public static async Task<IPaginatedList<TModel>> CreateAsync<TEntity, TModel>(
        IQueryable<TEntity> source,
        int pageNumber,
        int pageSize,
        Func<TEntity, TModel> transform,
        string pageQueryStringParameterName = "p")
    {
        // Prevent negative or zero indexing
        var idx = Math.Max(1, pageNumber);

        var totalCount = await source.TagWith("PaginatedList:Count").CountAsync().ConfigureAwait(false);

        IEnumerable<TEntity> items = totalCount > 0
            ? await source.TagWith("PaginatedList:Items").Skip((idx - 1) * pageSize).Take(pageSize).ToListAsync().ConfigureAwait(false)
            : Enumerable.Empty<TEntity>();

        return CreateList(items.Select(transform), totalCount, pageNumber, pageSize, pageQueryStringParameterName);
    }

    /// <summary>
    /// Creates a paginated list using the provided enumerable.
    /// </summary>
    /// <param name="source">The source enumerable. It should not have a skip and take applied.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The number of elements on a page.</param>
    /// <param name="pageQueryStringParameterName">The query string parameter name used to page this pager (defaults to "p")</param>
    /// <typeparam name="T"></typeparam>
    public static IPaginatedList<T> Create<T>(IReadOnlyList<T> source, int pageNumber, int pageSize, string pageQueryStringParameterName = "p")
    {
        // Prevent negative indexing
        var idx = Math.Max(1, pageNumber);

        var enumerable = source as T[] ?? source.ToArray();
        var items = enumerable.Skip((idx - 1) * pageSize).Take(pageSize).ToList();
        var count = enumerable.Length;

        return CreateList(items, count, pageNumber, pageSize, pageQueryStringParameterName);
    }

    /// <summary>
    /// Creates a paginated list using the provided enumerable.
    /// </summary>
    /// <param name="source">The source enumerable. It should not have a skip and take applied.</param>
    /// <param name="count">Total count of pages.</param>
    /// <param name="pageNumber">The 1-based page index.</param>
    /// <param name="pageSize">The number of elements on a page.</param>
    /// <param name="pageQueryStringParameterName">The query string parameter name used to page this pager (defaults to "p")</param>
    /// <typeparam name="T"></typeparam>
    public static IPaginatedList<T> CreateList<T>(IEnumerable<T> source, int count, int pageNumber, int pageSize, string pageQueryStringParameterName = "p")
    {
        return new PaginatedList<T>(source, count, pageNumber, pageSize, pageQueryStringParameterName);
    }

    /// <summary>
    /// Creates a new <see cref="IPaginatedList"/> by executing the <paramref name="mapper"/> function on each item in <paramref name="source"/>
    /// </summary>
    /// <param name="source">The source list.</param>
    /// <param name="mapper">The function to use to map each item.</param>
    /// <param name="pageQueryStringParameterName">The query string parameter name used to page this pager (defaults to "p")</param>
    /// <typeparam name="TSource">The type of items in the source list.</typeparam>
    /// <typeparam name="TTarget">The type of items in the result list.</typeparam>
    /// <returns>A <see cref="IPaginatedList"/> where each source item has been converted to the target type using the <paramref name="mapper"/> function.</returns>
    // This is named 'Map' to avoid conflicting with the 'Select' extension method on IEnumerable.
    // It behaves the same as 'Select', but is specialized to propagate the other paginated list metadata.
    public static IPaginatedList<TTarget> Map<TSource, TTarget>(this IPaginatedList<TSource> source, Func<TSource, TTarget> mapper, string pageQueryStringParameterName = "p")
    {
        var list = source.Select(mapper).ToList();
        return new PaginatedList<TTarget>(list, source.TotalCount, source.PageNumber, source.PageSize, pageQueryStringParameterName);
    }
}

public class PaginatedList<T> : PartialList<T>, IPaginatedList<T>
{
    const int CurrentPagePadding = 2;
    public int PageNumber { get; }
    public int TotalPages { get; }
    public int PageSize { get; }
    public string PageQueryStringParameterName { get; set; }

    public PaginatedList(IEnumerable<T> items, int totalCount, int pageNumber, int pageSize, string pageQueryStringParameterName = "p") : base(items, totalCount)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        PageQueryStringParameterName = pageQueryStringParameterName;
    }

    public IEnumerable<IPage> Pages
    {
        get {
            // If we're on page 7, the middle range is [5],[6],[*7*],[8],[9]
            int middleStartPageNumber = Math.Max(CurrentPagePadding + 1, PageNumber - CurrentPagePadding);
            int pagesInFirstDivider = middleStartPageNumber - CurrentPagePadding - 1;
            int middleEndPageNumber = Math.Min(Math.Max(PageNumber, CurrentPagePadding + 1) + CurrentPagePadding, TotalPages);
            int startEndRange = TotalPages - CurrentPagePadding + 1;
            int pagesInSecondDivider = startEndRange - middleEndPageNumber - 1;

            // We always show page 1 and 2
            for (int i = 1; i <= Math.Min(CurrentPagePadding, TotalPages); i++)
            {
                yield return new PaginatedPage(i, i == PageNumber);
            }

            if (pagesInFirstDivider == 1)
            {
                // Special case, the divider would only contain 1 page. Might as well show the page.
                yield return new PaginatedPage(middleStartPageNumber - 1, middleStartPageNumber - 1 == PageNumber);
            }
            else if (pagesInFirstDivider > 1)
            {
                yield return new PaginatedDivider();
            }

            int middleStart = Math.Min(middleStartPageNumber, Math.Max(TotalPages - 4, 4));
            for (int i = middleStart; i <= middleEndPageNumber; i++)
            {
                yield return new PaginatedPage(i, i == PageNumber);
            }

            if (pagesInSecondDivider == 1)
            {
                yield return new PaginatedPage(middleEndPageNumber + 1, middleEndPageNumber + 1 == PageNumber);
            }
            else if (pagesInSecondDivider > 1)
            {
                yield return new PaginatedDivider();
            }

            int endRangeFirstPage = Math.Max(startEndRange, middleEndPageNumber + 1);

            for (int i = endRangeFirstPage; i <= TotalPages; i++)
            {
                yield return new PaginatedPage(i, i == PageNumber);
            }
        }
    }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}

public interface IPage
{
    public int Number { get; }
    public bool Current { get; }
}

[DebuggerDisplay("Page {Number}")]
public class PaginatedPage : IPage
{
    public PaginatedPage(int pageNumber, bool current)
    {
        Number = pageNumber;
        Current = current;
    }

    public int Number { get; }
    public bool Current { get; }
}

public class PaginatedDivider : PaginatedPage
{
    public PaginatedDivider() : base(-1, false)
    {
    }
}
