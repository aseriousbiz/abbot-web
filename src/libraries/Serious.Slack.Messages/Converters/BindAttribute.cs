using System;

namespace Serious.Slack.Converters;

/// <summary>
/// Attribute used to help us bind a block actions state value to a Record property.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class BindAttribute : Attribute
{
    /// <summary>
    /// The Block Id.
    /// </summary>
    public string BlockId { get; }

    /// <summary>
    /// The Action Id.
    /// </summary>
    public string? ActionId { get; }

    /// <summary>
    /// Constructs a <see cref="BindAttribute"/> with the specified block and action IDs.
    /// </summary>
    /// <param name="blockId">The block Id.</param>
    /// <param name="actionId">The action Id.</param>
    public BindAttribute(string blockId, string? actionId = null)
    {
        BlockId = blockId;
        ActionId = actionId;
    }
}
