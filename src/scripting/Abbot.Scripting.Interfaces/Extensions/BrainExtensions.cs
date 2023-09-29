using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Useful methods for working with Abbot's brain.
/// </summary>
public static class BrainExtensions
{
    /// <summary>
    /// Adds the specified <paramref name="item"/> to a <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-5.0">List{T}</see> stored in the brain. Behind the
    /// scenes this retrieves the list stored for the key. If there is none, it creates one. Then adds the item to
    /// the list, and then writes the list back to the brain.
    /// </summary>
    /// <param name="brain">The Bot brain. This is where skills can store and retrieve information.</param>
    /// <param name="key">The key of the list.</param>
    /// <param name="item">The item to add to the list.</param>
    /// <returns>Returns the <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-5.0">List{T}</see> associated to the <paramref name="key"/> with the added <paramref name="item"/>.</returns>
    public static async Task<List<T>> AddToListAsync<T>(this IBrain brain, string key, T item)
    {
        var list = await brain.GetListAsync<T>(key);
        list.Add(item);
        await brain.WriteAsync(key, list);
        return list;
    }

    /// <summary>
    /// Adds the specified <paramref name="item"/> to a <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-5.0">HashSet{T}</see> stored in the brain associated
    /// with the specified <paramref name="key" />.
    /// </summary>
    /// <remarks>
    /// This retrieves the set stored for the key. If there is none, it creates one. Then adds the item to
    /// the set, and then writes the set back to the brain.
    /// </remarks>
    /// <param name="brain">The Bot brain. This is where skills can store and retrieve information.</param>
    /// <param name="key">The key of the list.</param>
    /// <param name="item">The item to add to the list.</param>
    /// <returns><c>true</c> if the element is added to the <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-5.0">HashSet{T}</see>; <c>false</c> if the element is already present.</returns>
    public static async Task<bool> AddToHashSetAsync<T>(this IBrain brain, string key, T item)
    {
        var set = await brain.GetHashSetAsync<T>(key);
        var added = set.Add(item);
        if (added)
        {
            await brain.WriteAsync(key, set);
        }

        return added;
    }

    /// <summary>
    /// Removes the specified <paramref name="item"/> from a <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-5.0">List{T}</see> stored in the brain associated
    /// with the specified <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// This retrieves the list stored for the key. If there is none, it returns immediately. Otherwise it
    /// removes the specified item from the list.
    /// </remarks>
    /// <param name="brain">The Bot brain. This is where skills can store and retrieve information.</param>
    /// <param name="key">The key of the list.</param>
    /// <param name="item">The item to add to the list.</param>
    /// <returns><c>true</c> if item is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c> if item was not found in the List.</returns>
    public static async Task<bool> RemoveFromListAsync<T>(this IBrain brain, string key, T item)
    {
        var list = await brain.GetAsAsync<List<T>>(key);
        if (list is null)
        {
            return false;
        }
        var removed = list.Remove(item);
        if (removed)
        {
            await brain.WriteAsync(key, list);
        }

        return removed;
    }

    /// <summary>
    /// Removes an item by <paramref name="index"/> from a <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-5.0">List{T}</see> stored in the brain associated
    /// with the specified <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// This retrieves the list stored for the key. If there is none, it returns immediately. Otherwise it
    /// removes the specified item from the list.
    /// </remarks>
    /// <param name="brain">The Bot brain. This is where skills can store and retrieve information.</param>
    /// <param name="key">The key of the list.</param>
    /// <param name="index">The item to add to the list.</param>
    /// <returns><c>true</c> if item is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c> if <paramref name="index"/> is out of range.</returns>
    public static async Task<bool> RemoveAtFromListAsync<T>(this IBrain brain, string key, int index)
    {
        var list = await brain.GetAsAsync<List<T>>(key);
        if (list is null || index < 0 || index >= list.Count)
        {
            return false;
        }

        list.RemoveAt(index);
        await brain.WriteAsync(key, list);
        return true;
    }

    /// <summary>
    /// Removes the specified <paramref name="item"/> from a <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-5.0">HashSet{T}</see> stored in the brain associated
    /// with the specified <paramref name="key" />.
    /// </summary>
    /// <remarks>
    /// This retrieves the set stored for the key. If there is none, it returns false. Then it removes the item
    /// from the set and writes the set back to the brain.
    /// </remarks>
    /// <param name="brain">The Bot brain. This is where skills can store and retrieve information.</param>
    /// <param name="key">The key of the list.</param>
    /// <param name="item">The item to remove from the list.</param>
    /// <returns><c>true</c> if item is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c> if the item is not in the <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-5.0">HashSet{T}</see>.</returns>
    public static async Task<bool> RemoveFromHashSetAsync<T>(this IBrain brain, string key, T item)
    {
        var set = await brain.GetAsAsync<HashSet<T>>(key);
        if (set is null)
        {
            return false;
        }
        var removed = set.Remove(item);
        if (removed)
        {
            await brain.WriteAsync(key, set);
        }

        return removed;
    }

    /// <summary>
    /// Gets a List{T} from the brain. If it does not exist, returns a new <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-5.0">List{T}</see>.
    /// </summary>
    /// <param name="brain">The brain</param>
    /// <param name="key">The key</param>
    /// <returns>Returns the <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1?view=net-5.0">List{T}</see> associated with the <paramref name="key"/></returns>
    public static async Task<List<T>> GetListAsync<T>(this IBrain brain, string key)
    {
        return await brain.GetAsAsync(key, new List<T>());
    }

    /// <summary>
    /// Gets a HashSet{T} from the brain. If it does not exist, returns a new <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-5.0">HashSet{T}</see>.
    /// </summary>
    /// <param name="brain">The brain</param>
    /// <param name="key">The key</param>
    /// <returns>Returns the <see href="https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1?view=net-5.0">HashSet{T}</see> associated with the <paramref name="key"/></returns>
    public static async Task<HashSet<T>> GetHashSetAsync<T>(this IBrain brain, string key)
    {
        return await brain.GetAsAsync(key, new HashSet<T>());
    }
}
