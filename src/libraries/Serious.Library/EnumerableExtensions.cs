using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Serious.Text;

namespace Serious;

/// <summary>
/// Extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Given a list, returns a tuple of the list without the last item, and the last item.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <typeparam name="T">The type of each item.</typeparam>
    /// <returns>A tuple of the list without the last item and the last item.</returns>
    public static (IReadOnlyList<T>, T?) Tail<T>(this IReadOnlyList<T> items)
    {
        return items is { Count: 0 }
            ? (Array.Empty<T>(), default)
            : (items.SkipLast(1).ToList(), items[^1]);
    }

    /// <summary>
    /// Filters elements of two different types, applying the corresponding selector, and then returning
    /// the result type.
    /// </summary>
    /// <param name="enumerable">The enumerable.</param>
    /// <param name="oneSelector">The selector for the first type.</param>
    /// <param name="twoSelector">The selector for the second type.</param>
    /// <typeparam name="TBase">The common base type for the items in the enumerable.</typeparam>
    /// <typeparam name="TOne">The first filter type.</typeparam>
    /// <typeparam name="TTwo">The second filter type.</typeparam>
    /// <typeparam name="TResult">The resulting type.</typeparam>
    public static IEnumerable<TResult> OfTypes<TBase, TOne, TTwo, TResult>(
        this IEnumerable<object> enumerable,
        Func<TOne, TResult> oneSelector,
        Func<TTwo, TResult> twoSelector)
        where TOne : TBase
        where TTwo : TBase
    {
        foreach (var item in enumerable)
        {
            if (item is TOne one)
            {
                yield return oneSelector(one);
            }
            else if (item is TTwo two)
            {
                yield return twoSelector(two);
            }
        }
    }

    /// <summary>
    /// Filters elements of two different types, applying the corresponding selector, and then returns
    /// an enumerable of objects.
    /// </summary>
    /// <param name="enumerable">The enumerable.</param>
    /// <param name="oneSelector">The selector for the first type.</param>
    /// <param name="twoSelector">The selector for the second type.</param>
    /// <typeparam name="TOne">The first filter type.</typeparam>
    /// <typeparam name="TTwo">The second filter type.</typeparam>
    public static IEnumerable<object> OfTypes<TOne, TTwo>(
        this IEnumerable<object> enumerable,
        Func<TOne, object> oneSelector,
        Func<TTwo, object> twoSelector)
            where TOne : notnull where TTwo : notnull
        => enumerable.OfTypes<object, TOne, TTwo, object>(oneSelector, twoSelector);

    /// <summary>
    /// Returns an enumerable of <see cref="Func{TResult}"/>.
    /// </summary>
    /// <param name="enumerable">The enumerable.</param>
    /// <param name="selector">The selector which will remain a func.</param>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <returns></returns>
    public static IEnumerable<Func<TResult>> SelectFunc<TSource, TResult>(
        this IEnumerable<TSource> enumerable, Func<TSource, TResult> selector)
    {
        return enumerable.Select(e => new Func<TResult>(() => selector(e)));
    }

    /// <summary>
    /// Returns a new <see>
    ///     <cref>IEnumerable{(int, T)}</cref>
    /// </see>
    /// that contains the index for each element in the collection.
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable{T}"/>.</param>
    /// <typeparam name="T">The type of objects to enumerate.</typeparam>
    /// <returns>A <see>
    ///         <cref>IEnumerable{(int, T)}</cref>
    ///     </see>
    ///     that yields the original items along with their index.</returns>
    public static IEnumerable<(int Index, T Value)> Enumerate<T>(this IEnumerable<T> enumerable)
    {
        // Inspired by https://doc.rust-lang.org/stable/std/iter/trait.Iterator.html#method.enumerate
        return enumerable.Select((value, index) => (Index: index, Value: value));
    }

    /// <summary>
    /// Wraps an <see cref="IEnumerable{T}"/> in a <see cref="IReadOnlyList{T}"/>.
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable{T}"/>.</param>
    /// <typeparam name="T">The type of objects to enumerate.</typeparam>
    /// <returns>The <see cref="IReadOnlyList{T}"/> that wraps the enumerable.</returns>
    public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> enumerable)
    {
        return new ReadOnlyCollection<T>(enumerable.ToList());
    }

    /// <summary>
    /// Wraps an <see cref="IEnumerable{T}"/> in a <see cref="ICollection{T}"/>.
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable{T}"/>.</param>
    /// <typeparam name="T">The type of objects to enumerate.</typeparam>
    /// <returns>The <see cref="ICollection{T}"/> that wraps the enumerable.</returns>
    public static ICollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> enumerable)
    {
        return new ReadOnlyCollection<T>(enumerable.ToList());
    }

    public static IReadOnlyDictionary<TKey, TItem> ToReadOnlyDictionary<TKey, TItem>(
        this IEnumerable<TItem> enumerable, Func<TItem, TKey> keySelector) where TKey : notnull =>
        ToReadOnlyDictionary(enumerable, keySelector, i => i);

    public static IReadOnlyDictionary<TKey, TValue> ToReadOnlyDictionary<TKey, TValue, TItem>(
        this IEnumerable<TItem> enumerable, Func<TItem, TKey> keySelector, Func<TItem, TValue> valueSelector) where TKey : notnull
    {
        return enumerable.ToDictionary(keySelector, valueSelector).ToReadOnlyDictionary();
    }

    /// <summary>
    /// Projects each element of a sequence into a new form, but passes the result of the previous iteration to the
    /// next iteration.
    /// </summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the result of the previous transform.</param>
    /// <param name="seed">The initial value to pass to the first iteration.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <param name="selector"/>.</typeparam>
    /// <returns>An <see cref="IEnumerable{TResult}"/></returns>
    public static IEnumerable<TResult> SelectWithPrevious<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult, TResult> selector,
        TResult seed)
    {
        foreach (var item in source)
        {
            seed = selector(item, seed);
            yield return seed;
        }
    }

    /// <summary>
    /// Enumerates a sequence and for each element, includes the next element. If the element is the last element in
    /// the sequence, the next element will be null.
    /// </summary>
    /// <remarks>
    /// Note that this differs from the Reactive Extension Pairwise implementation yields N-1 prev/current pairs
    /// </remarks>
    /// <param name="source">The source sequence.</param>
    /// <typeparam name="TElement">The element type.</typeparam>
    public static IEnumerable<(TElement, TElement?)> SelectWithNext<TElement>(this IEnumerable<TElement> source)
    {
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            yield break;
        }

        var previous = enumerator.Current;
        while (enumerator.MoveNext())
        {
            yield return (previous, enumerator.Current);
            previous = enumerator.Current;
        }

        yield return (previous, default);
    }

    /// <summary>
    /// Projects each element of a sequence into a new form, but passes the result of the previous iteration to the
    /// next iteration.
    /// </summary>
    /// <param name="source">A sequence of values to invoke a transform function on.</param>
    /// <param name="selector">A transform function to apply to each source element; the second parameter of the function represents the result of the previous transform.</param>
    /// <param name="seed">The initial value to pass to the first iteration.</param>
    /// <typeparam name="TSource">The type of the elements of <paramref name="source"/>.</typeparam>
    /// <typeparam name="TResult">The type of the value returned by <param name="selector"/>.</typeparam>
    /// <returns>An <see cref="IEnumerable{TResult}"/></returns>
    public static async IAsyncEnumerable<TResult> SelectWithPrevious<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, TResult, Task<TResult>> selector,
        TResult seed)
    {
        foreach (var item in source)
        {
            seed = await selector(item, seed);
            yield return seed;
        }
    }

    /// <summary>
    /// Wraps a dictionary with a <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <param name="dictionary">The dictionary to wrap.</param>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <returns></returns>
    public static IReadOnlyDictionary<TKey, TValue> ToReadOnlyDictionary<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        return new ReadOnlyDictionary<TKey, TValue>(dictionary);
    }

    /// <summary>
    /// Returns the last index of an item in a list that matches a condition.
    /// </summary>
    /// <param name="items">The items in a list.</param>
    /// <param name="condition">The condition.</param>
    /// <typeparam name="T">The item type.</typeparam>
    /// <returns>The last index where the condition is true. If the condition is never true, returns 0.</returns>
    public static int GetLastIndexOf<T>(this IReadOnlyList<T> items, Func<T, bool> condition)
    {
        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (condition(items[i]))
                return i;
        }

        return -1;
    }

    public static IEnumerable<T> WhereFuzzyMatch<T>(
        this IEnumerable<T> enumerable,
        Func<T, string> searchSelector,
        string pattern)
    {
        return enumerable.Where(item => searchSelector(item).FuzzyMatch(pattern));
    }

    public static IEnumerable<T> WhereFuzzyMatch<T>(
        this IEnumerable<T> enumerable,
        Func<T, string> searchSelector,
        Func<T, string> anotherSelector,
        string pattern)
    {
        return enumerable.Where(item => searchSelector(item).FuzzyMatch(pattern)
            || anotherSelector(item).FuzzyMatch(pattern));
    }

    public static IEnumerable<string> WhereFuzzyMatch(
        this IEnumerable<string> enumerable,
        string pattern)
    {
        return enumerable.Where(item => item.FuzzyMatch(pattern));
    }

    static readonly Func<object?, bool> NotNullTest = x => x is not null;

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : class
    {
        return source.Where((Func<T?, bool>)NotNullTest)!;
    }

    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        where T : struct
    {
        return source.Where(x => x.HasValue).Select(x => x.GetValueOrDefault());
    }

    /// <summary>
    /// Retrieves a sequence of elements from root to descendant from a set of elements that have parent child
    /// relationships.
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable{T}"/>.</param>
    /// <param name="keySelector">Given an element, returns the key to order by.</param>
    /// <param name="parentKeySelector">Given an element, returns the key for the parent. The key is what we order by.</param>
    /// <param name="root">The root element of the sequence to get. The method will retrieve all descendants of this element.</param>
    /// <typeparam name="TItem">The type of objects to order.</typeparam>
    /// <typeparam name="TKey">The key type of the property of the objects to order that we order by.</typeparam>
    /// <returns>The sorted <see cref="IEnumerable{T}"/>.</returns>
    public static IEnumerable<TItem> GetLineage<TItem, TKey>(
        this IEnumerable<TItem> enumerable,
        Func<TItem, TKey> keySelector,
        Func<TItem, TKey?> parentKeySelector,
        TItem root)
    {
        var seen = new HashSet<TKey>
        {
            keySelector(root)
        };
        var results = new List<TItem> { root };
        // We can ignore CS8714 because we already make sure key is not null in the Where clause.
#pragma warning disable CS8714
        var parentChildGroups = enumerable
            .Where(item => parentKeySelector(item) is not null)
            .GroupBy(parentKeySelector)
            .ToDictionary(g => g.Key!);
#pragma warning restore CS8714

        void AddChildBatches(IEnumerable<TItem> batch)
        {
            var queue = new List<TItem>();
            foreach (var item in batch)
            {
                var key = keySelector(item);
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    results.Add(item);
                    queue.Add(item);
                }
            }

            foreach (var parent in queue)
            {
                if (parentChildGroups.TryGetValue(keySelector(parent), out var children))
                {
                    AddChildBatches(children);
                }
            }
        }

        var children = parentChildGroups[keySelector(root)];
        AddChildBatches(children);
        return results;
    }

    /// <summary>
    /// Given a sequence of items, retrieves all the lineages in the sequence. Lineages are sequences that start
    /// with a root ancestor with no parent and end with all descendants of that element.
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable{T}"/>.</param>
    /// <param name="keySelector">Given an element, returns the order by key.</param>
    /// <param name="parentKeySelector">Given an element, returns the key for the parent. The key is what we order by.</param>
    /// <typeparam name="TItem">The type of objects to order.</typeparam>
    /// <typeparam name="TKey">The key type of the property of the objects to order that we order by.</typeparam>
    /// <returns>The sorted <see cref="IEnumerable{T}"/>.</returns>
    public static IEnumerable<IEnumerable<TItem>> GetLineages<TItem, TKey>(
        this IEnumerable<TItem> enumerable,
        Func<TItem, TKey> keySelector,
        Func<TItem, TKey?> parentKeySelector)
    {
        var items = enumerable.ToList();
        var roots = items.Where(i => parentKeySelector(i) is null).ToList();
        return roots.Select(root => items.GetLineage(keySelector, parentKeySelector, root));
    }

    /// <summary>
    /// Flattens a hierarchy into a flat enumerable.
    /// </summary>
    /// <param name="source">The source enumerable.</param>
    /// <param name="childrenSelector">A Func used to retrieve children from the element. If the element does not have children, this should return an empty enumerable.</param>
    /// <typeparam name="TSource">The type of element in the source enumerable.</typeparam>
    /// <returns>An <see cref="IEnumerable{T}"/> with the flattened hierarchy.</returns>
    public static IEnumerable<TSource> Flatten<TSource>(
        this IEnumerable<TSource> source,
        Func<TSource, IEnumerable<TSource>> childrenSelector)
    {
        foreach (var element in source)
        {
            yield return element;
            var children = childrenSelector(element);
            foreach (var child in children.Flatten(childrenSelector))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Given a set of groups, ensures there's a group for every key.
    /// </summary>
    /// <remarks>
    /// When grouping by a key, the source might not contain a value for every possible key. This method ensures that
    /// there's a group for every key by adding an empty group for every key that doesn't have a group.
    /// </remarks>
    /// <param name="groups"></param>
    /// <param name="keys"></param>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TItem"></typeparam>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public static IEnumerable<IGrouping<TKey, TItem>> EnsureGroups<TKey, TItem>(
        this IEnumerable<IGrouping<TKey, TItem>> groups,
        IEnumerable<TKey> keys)
    {
        var allGroups = keys.Select(k => new EmptyGrouping<TKey, TItem>(k));
        return groups.UnionBy(allGroups, g => g.Key);
    }

    /// <summary>
    /// Groups the enumerable by the given key selector, and applies an aggregator to each group, carrying over a
    /// value from the previous group to the next group.
    /// </summary>
    /// <param name="groups">The ordered set of groups to aggregate across.</param>
    /// <param name="groupValueAggregator">A function applied to the group to aggregate the group into a value.</param>
    /// <param name="carryoverGroupAggregator">A function applied to each group to calculate the carryover to the next group.</param>
    /// <param name="seed">The initial value.</param>
    /// <typeparam name="TKey">The type of the key for the groups.</typeparam>
    /// <typeparam name="TAccumulate">The type of the accumulator value.</typeparam>
    /// <typeparam name="TElement">The type of elements in the <paramref name="groups"/>.</typeparam>
    /// <returns>An enumeration of each group's accumulated value.</returns>
    public static IEnumerable<KeyValuePair<TKey, TAccumulate>> AggregateGroups<TKey, TAccumulate, TElement>(
        // We want to remind callers to make sure the groups are ordered in the order they want aggregation to occur.
        // ReSharper disable once ParameterTypeCanBeEnumerable.Global
        this IOrderedEnumerable<IGrouping<TKey, TElement>> groups,
        Func<TAccumulate, IGrouping<TKey, TElement>, TAccumulate> groupValueAggregator,
        Func<TAccumulate, IGrouping<TKey, TElement>, TAccumulate> carryoverGroupAggregator,
        TAccumulate seed) where TKey : notnull
    {
        foreach (var group in groups)
        {
            var currentValue = groupValueAggregator(seed, group);
            seed = carryoverGroupAggregator(currentValue, group);
            yield return new KeyValuePair<TKey, TAccumulate>(group.Key, currentValue);
        }
    }

    /// <summary>
    /// Groups the enumerable by the given key selector, and applies an aggregator to each group, carrying over a
    /// value from the previous group to the next group. This is a special case of <see cref="AggregateGroups{TKey,TAccumulate,TElement}"/>
    /// that works for aggregating integers.
    /// </summary>
    /// <param name="groups">The ordered set of groups to aggregate across.</param>
    /// <param name="groupValueAggregator">A function applied to the group to aggregate the group into a value. The carry over will be added to this automatically.</param>
    /// <param name="carryoverAdjustmentAggregator">A function applied to each group to calculate the adjustment to the current value to carry over to the next group.</param>
    /// <param name="seed">The initial value.</param>
    /// <typeparam name="TKey">The type of the key for the groups.</typeparam>
    /// <typeparam name="TElement">The type of elements in the <paramref name="groups"/>.</typeparam>
    /// <returns>An enumeration of each group's accumulated value.</returns>
    public static IEnumerable<KeyValuePair<TKey, int>> AggregateGroups<TKey, TElement>(
        // We want to remind callers to make sure the groups are ordered in the order they want aggregation to occur.
        // ReSharper disable once ParameterTypeCanBeEnumerable.Global
        this IOrderedEnumerable<IGrouping<TKey, TElement>> groups,
        Func<IGrouping<TKey, TElement>, int> groupValueAggregator,
        Func<IGrouping<TKey, TElement>, int> carryoverAdjustmentAggregator,
        int seed) where TKey : notnull
    {
        return groups.AggregateGroups(
            (carry, group) => carry + groupValueAggregator(group),
            (currentValue, group) => currentValue + carryoverAdjustmentAggregator(group),
            seed);
    }

    /// <summary>
    /// Adds a range of items to the target <see cref="ICollection{T}"/>.
    /// </summary>
    /// <param name="target">The collection to add to.</param>
    /// <param name="itemsToAdd">The items to add.</param>
    /// <typeparam name="T"></typeparam>
    public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> itemsToAdd)
    {
        if (target is List<T> list)
        {
            list.AddRange(itemsToAdd);
        }
        else
        {
            foreach (var item in itemsToAdd)
            {
                target.Add(item);
            }
        }
    }

    /// <summary>
    /// Sums a set of <see cref="TimeSpan"/>s.
    /// </summary>
    /// <param name="enumerable">A set of <see cref="TimeSpan"/>.</param>
    public static TimeSpan Sum(this IEnumerable<TimeSpan> enumerable)
    {
        return enumerable.Aggregate(TimeSpan.Zero, (current, timeSpan) => current + timeSpan);
    }

    /// <summary>
    /// Makes updates to the source collection to so that in the end it results with the elements specified by the
    /// target collection.
    /// </summary>
    /// <param name="source">The source collection that will be modified.</param>
    /// <param name="target">The target collection that identifies how the source collection should end up.</param>
    /// <param name="comparer">Compares the source and target element and returns true if they represent the same item.</param>
    /// <param name="lookupSourceToAdd">Given a target element, retrieves a source element to add.</param>
    /// <param name="addItem">Method used to add an item.</param>
    /// <param name="removeItem">Method used to remove an item.</param>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TTarget">The target element type.</typeparam>
    /// <returns><c>true</c> if any changes were made.</returns>
    public static bool Sync<TSource, TTarget>(
        this ICollection<TSource> source,
        IEnumerable<TTarget> target,
        Func<TSource, TTarget, bool> comparer,
        Func<TTarget, TSource> lookupSourceToAdd,
        Action<ICollection<TSource>, TSource>? addItem = null,
        Action<ICollection<TSource>, TSource>? removeItem = null)
    {
        addItem ??= (c, e) => c.Add(e);
        removeItem ??= (c, e) => c.Remove(e);

        var itemsToRemove = source
            .Where(s => !target.Any(t => comparer(s, t)))
            .ToList();
        var itemsToAdd = target
            .Where(t => !source.Any(s => comparer(s, t)))
            .ToList();
        bool changed = false;
        foreach (var itemToRemove in itemsToRemove)
        {
            changed = true;
            removeItem(source, itemToRemove);
        }

        foreach (var itemToAdd in itemsToAdd)
        {
            changed = true;
            addItem(source, lookupSourceToAdd(itemToAdd));
        }

        return changed;
    }

    /// <summary>
    /// Produces a new sequence where the delimiter item is in between each item in the original sequence. Similar
    /// to what string.Join does for strings.
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable{T}"/>.</param>
    /// <param name="delimiter">The delimiter item.</param>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <typeparam name="TDelimiter">The delimiter type.</typeparam>
    /// <returns>A new sequence with the delimiter item in between each item.</returns>
    public static IEnumerable<TItem> Delimit<TItem, TDelimiter>(
        this IEnumerable<TItem> enumerable,
        TDelimiter delimiter) where TDelimiter : TItem
    {
        using var enumerator = enumerable.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            yield break; // Empty sequence
        }

        yield return enumerator.Current;

        while (enumerator.MoveNext())
        {
            yield return delimiter;
            yield return enumerator.Current;
        }
    }

    /// <summary>
    /// Produces a new sequence where the delimiter item is in between each item in the original sequence. Similar
    /// to what string.Join does for strings.
    /// </summary>
    /// <param name="enumerable">The <see cref="IEnumerable{T}"/>.</param>
    /// <param name="delimiter">The delimiter item.</param>
    /// <typeparam name="TItem">The item type.</typeparam>
    /// <returns>A new sequence with the delimiter item in between each item.</returns>
    public static IEnumerable<TItem> Delimit<TItem>(
        this IEnumerable<TItem> enumerable,
        TItem delimiter) => enumerable.Delimit<TItem, TItem>(delimiter);

    public static bool Sync<TSource, TKey>(
        this ICollection<TSource> source,
        IEnumerable<TSource> target,
        Func<TSource, TKey> keySelector,
        Func<TSource, TSource> lookupSourceToAdd,
        Action<ICollection<TSource>, TSource>? addItem = null,
        Action<ICollection<TSource>, TSource>? removeItem = null) where TKey : notnull =>
        source.Sync(target,
            (s, t) => keySelector(s).Equals(keySelector(t)),
            lookupSourceToAdd,
            addItem,
            removeItem);

    public static bool Sync<TSource, TKey>(
        this ICollection<TSource> source,
        IEnumerable<TSource> target,
        Func<TSource, TKey> keySelector,
        Action<ICollection<TSource>, TSource>? addItem = null,
        Action<ICollection<TSource>, TSource>? removeItem = null) where TKey : notnull =>
        source.Sync(target, keySelector, itemToAdd => itemToAdd, addItem, removeItem);

    record EmptyGrouping<TKey, TElement>(TKey Key) : IGrouping<TKey, TElement>
    {
        public IEnumerator<TElement> GetEnumerator() => Enumerable.Empty<TElement>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public static double? Median(this IEnumerable<double> values)
    {
        var sortedValues = values.OrderBy(x => x).ToArray();
        int count = sortedValues.Length;

        if (count == 0)
        {
            return null;
        }

        int midIndex = count / 2;

        if (count % 2 == 0)
        {
            // Average of two middle elements
            return (sortedValues[midIndex - 1] + sortedValues[midIndex]) / 2;
        }
        else
        {
            // Middle element
            return sortedValues[midIndex];
        }
    }

    public static BasicStatistics? CalculateBasicStatistics(this IEnumerable<double> values)
    {
        var list = values.ToArray();
        int count = list.Length;
        if (count is 0)
        {
            return null;
        }

        //Compute the Average
        double avg = list.Average();

        //Perform the Sum of (value-avg)^2
        double sum = list.Sum(d => (d - avg) * (d - avg));

        //Put it all together
        double stdDev = Math.Sqrt(sum / count);

        double max = list.Max();

        double min = list.Min();

        double? median = list.Median();

        return new BasicStatistics(avg, median.GetValueOrDefault(), stdDev, min, max, count);
    }
}

public record BasicStatistics(double Average, double Median, double StandardDeviation, double Min, double Max, int Count);

/// <summary>
/// Used in places where you have to pass an <see cref="IEqualityComparer{T}"/> but you just want to pass a
/// Func.
/// </summary>
/// <typeparam name="TKey">The key type.</typeparam>
public class FuncEqualityComparer<TKey> : IEqualityComparer<TKey>
{
    readonly Func<TKey, TKey, bool> _comparer;

    public FuncEqualityComparer(Func<TKey, TKey, bool> comparer)
    {
        _comparer = comparer;
    }

    public bool Equals(TKey? x, TKey? y)
    {
        return (x, y) switch
        {
            (null, null) => true,
            (_, null) => false,
            (null, _) => false,
            _ => _comparer(x, y)
        };
    }

    public int GetHashCode(TKey obj)
    {
        return obj?.GetHashCode() ?? 0;
    }
}
