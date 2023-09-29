using System;
using System.Collections.Generic;
using System.Linq;

namespace Serious;

/// <summary>
/// Represents a list that is known to already be sorted. Implementations are not responsible for doing the sorting.
/// </summary>
/// <remarks>
/// This comes out of a need to have an ordered list that is known to be sorted and already evaluated (aka not-lazy).
/// </remarks>
/// <typeparam name="TElement">The type of element in the list.</typeparam>
public interface IReadOnlyOrderedList<out TElement> : IOrderedEnumerable<TElement>, IReadOnlyList<TElement>
{
}

/// <summary>
/// Implementation of <see cref="IReadOnlyOrderedList{TElement}"/> that is based on a <see cref="List{T}"/>.
/// </summary>
/// <typeparam name="TElement">The element type.</typeparam>
public class ReadOnlyOrderedList<TElement> : List<TElement>, IReadOnlyOrderedList<TElement>
{
    // ReSharper disable once ParameterTypeCanBeEnumerable.Local
    public ReadOnlyOrderedList(IOrderedEnumerable<TElement> collection) : base(collection)
    {
    }

    public IOrderedEnumerable<TElement> CreateOrderedEnumerable<TKey>(
        Func<TElement, TKey> keySelector,
        IComparer<TKey>? comparer,
        bool descending)
    {
        return descending
            ? this.OrderByDescending(keySelector, comparer)
            : this.OrderBy(keySelector, comparer);
    }
}

public static class ReadOnlyOrderedListExtensions
{
    public static IReadOnlyOrderedList<TElement> ToReadOnlyOrderedList<TElement>(
        this IOrderedEnumerable<TElement> enumerable)
    {
        return new ReadOnlyOrderedList<TElement>(enumerable);
    }
}
