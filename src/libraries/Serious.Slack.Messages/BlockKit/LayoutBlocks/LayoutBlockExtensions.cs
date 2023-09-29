using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;

namespace Serious.BlockKit.LayoutBlocks;

/// <summary>
/// Extension methods for working with <see cref="ILayoutBlock"/> and lists of <see cref="ILayoutBlock"/>s.
/// </summary>
public static class LayoutBlockExtensions
{
    /// <summary>
    /// Finds a block in the provided list with the specified block ID.
    /// </summary>
    /// <param name="blocks">The list of blocks to search.</param>
    /// <param name="blockId">The block ID to search for.</param>
    /// <returns>The <see cref="ILayoutBlock"/> with the specified ID, if found. Or, if the block could not be found, <c>null</c>.</returns>
    public static ILayoutBlock? FindBlockById(this IEnumerable<ILayoutBlock> blocks, string blockId) =>
        blocks.FirstOrDefault(b => b.BlockId == blockId);

    /// <summary>
    /// Finds a block in the provided list with the specified block ID and replaces it using the replace function.
    /// </summary>
    /// <param name="blocks">The list of blocks to search.</param>
    /// <param name="blockId">The block ID to search for.</param>
    /// <param name="replace">Used to replace the found block.</param>
    /// <returns>The index of the found block, or -1 if not found.</returns>
    public static int ReplaceBlockById<T>(
        this IList<ILayoutBlock> blocks,
        string blockId,
        Func<T, T> replace) where T : class, ILayoutBlock
    {
        var found = blocks.FindBlockById<T>(blockId);
        if (found is not null)
        {
            var existingIndex = blocks.IndexOf(found);
            blocks.RemoveAt(existingIndex);
            blocks.Insert(existingIndex, replace(found));
            return existingIndex;
        }

        return -1;
    }


    /// <summary>
    /// Finds a block in the provided list with the specified block ID and removes it.
    /// </summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="blockId">The block Id to search for.</param>
    /// <typeparam name="T"></typeparam>
    public static void RemoveBlockById<T>(this IList<ILayoutBlock> blocks, string blockId)
    {
        var found = blocks.FindBlockById(blockId);
        if (found != null)
        {
            blocks.Remove(found);
        }
    }

    /// <summary>
    /// Finds a block in the provided list with the specified block ID, and attempts to cast it to <typeparamref name="T"/>
    /// </summary>
    /// <param name="blocks">The list of blocks to search.</param>
    /// <param name="blockId">The block ID to search for.</param>
    /// <typeparam name="T">The type to attempt to cast the located block to.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/> if the block was found. Or, if the block could not be found or was not of type <typeparamref name="T"/>, <c>null</c>.</returns>
    public static T? FindBlockById<T>(this IEnumerable<ILayoutBlock> blocks, string blockId)
        where T : class, ILayoutBlock =>
        FindBlockById(blocks, blockId) as T;

    /// <summary>
    /// Finds the block in the provided list with the specified block ID, and attempts to cast it to an
    /// <see cref="Input"/> and then attempts to cast the <see cref="Input.Element"/> property to <typeparamref name="T"/>
    /// </summary>
    /// <param name="blocks">The list of blocks to search.</param>
    /// <param name="blockId">The block ID to search for.</param>
    /// <typeparam name="T">The type to attempt to cast the located <see cref="Input"/> block's element property to.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/> if the block was found. Or, if the block could not be found or was not of type <typeparamref name="T"/>, <c>null</c>.</returns>
    public static T? FindInputElementByBlockId<T>(this IEnumerable<ILayoutBlock> blocks, string blockId)
        where T : class, IInputElement =>
        FindBlockById<Input>(blocks, blockId)?.Element as T;

    /// <summary>
    /// Finds the block in the provided list with the specified block ID, and attempts to cast it to a
    /// <see cref="Section"/> and then attempts to cast the <see cref="Section.Accessory"/> property to <typeparamref name="T"/>
    /// </summary>
    /// <param name="blocks">The list of blocks to search.</param>
    /// <param name="blockId">The block ID to search for.</param>
    /// <typeparam name="T">The type to attempt to cast the located <see cref="Input"/> block's element property to.</typeparam>
    /// <returns>An instance of <typeparamref name="T"/> if the block was found. Or, if the block could not be found or was not of type <typeparamref name="T"/>, <c>null</c>.</returns>
    public static T? FindSectionAccessoryElementByBlockId<T>(this IEnumerable<ILayoutBlock> blocks, string blockId)
        where T : class, IInputElement =>
        FindBlockById<Section>(blocks, blockId)?.Accessory as T;
}
