using System;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

namespace Serious.Payloads;

/// <summary>
/// Extensions for extracting useful information out of interaction payloads.
/// </summary>
public static class PayloadExtensions
{
    /// <summary>
    /// Extracts the value of the <see cref="IValueElement"/> at the specified blockId and actionId.
    /// </summary>
    /// <param name="state">The <see cref="BlockActionsState"/> to extract values from.</param>
    /// <param name="blockId">The ID of the block containing the element to extract values from.</param>
    /// <param name="actionId">The action ID of the element to extract values from. Can be <c>null</c> if (and only if) the block contains only a single element.</param>
    /// <returns>The value of the specified element, or <c>null</c> if the element could not be found or is empty.</returns>
    public static string? ValueOrDefault(this BlockActionsState state, string blockId, string? actionId = null) =>
        state.TryGetAs<IValueElement>(blockId, actionId, out var valueElement) ? valueElement.Value : null;

    /// <summary>
    /// Extracts the value of the <see cref="IValueElement"/> at the specified blockId and actionId.
    /// </summary>
    /// <param name="state">The <see cref="BlockActionsState"/> to extract values from.</param>
    /// <param name="blockId">The ID of the block containing the element to extract values from.</param>
    /// <param name="actionId">The action ID of the element to extract values from. Can be <c>null</c> if (and only if) the block contains only a single element.</param>
    /// <returns>The value of the specified element.</returns>
    /// <exception cref="InvalidOperationException">Thrown if if the element could not be found or is empty</exception>
    public static string RequireValue(this BlockActionsState state, string blockId, string? actionId = null) =>
        state.ValueOrDefault(blockId, actionId) ?? throw new InvalidOperationException($"No value found for block '{blockId}' and action '{actionId}'. Slack should have validated it.");
}
