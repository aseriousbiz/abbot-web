using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Serious.Slack;

namespace Serious.Abbot.Events;

/// <summary>
/// Base class for private metadata passed to views. Metadata can be very specific to the view, which is why we don't
/// have a generic concrete type for this. Instead, we provide the abstract base class to encapsulate the pattern.
/// </summary>
/// <remarks>
/// Records have a default synthesized implementation of <see cref="ToString"/>. Thus every implementation of
/// this class has to override that method and call the base implementation.
/// <code>
/// public override string ToString() => base.ToString();
/// </code>
/// </remarks>
public abstract record PrivateMetadataBase
{
    /// <summary>
    /// Attempts to split the metadata string into <paramref name="partCount"/> parts using <c>|</c> as the delimiter.
    /// It only returns true if it can split the string into the exact number of non-empty parts.
    /// </summary>
    /// <param name="privateMetadata">The metadata string.</param>
    /// <param name="partCount">The number of parts to split into.</param>
    /// <param name="parts">The resulting parts.</param>
    /// <returns><c>true</c> if the parts could be parsed. Otherwise <c>false</c>.</returns>
    protected static bool TrySplitParts(
        string? privateMetadata,
        int partCount,
        [NotNullWhen(true)] out IReadOnlyList<string>? parts)
    {
        parts = null;

        if (privateMetadata is null)
        {
            return false;
        }
        if (privateMetadata.Length > ViewBase.PrivateMetadataMaxLength)
        {
            throw new InvalidOperationException($"Private metadata is too damn long! It can only be {ViewBase.PrivateMetadataMaxLength} characters:\n{privateMetadata}");
        }
        if (privateMetadata.Split(new[] { '|' }, partCount) is { } splitParts && splitParts.Length == partCount)
        {
            parts = splitParts;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Formats the properties as a delimited string.
    /// </summary>
    /// <returns>A delimited string.</returns>
    protected abstract IEnumerable<string> GetValues();

    /// <summary>
    /// Returns a delimited string suitable to store in the <c>private_metadata</c> field of a view.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        var privateMetadata = string.Join('|', GetValues());
        if (privateMetadata.Length > ViewBase.PrivateMetadataMaxLength)
        {
#pragma warning disable CA1065
            throw new InvalidOperationException($"Private metadata is too damn long! It can only be {ViewBase.PrivateMetadataMaxLength} characters:\n{privateMetadata}");
#pragma warning restore CA1065
        }

        return privateMetadata;
    }

    public static implicit operator string(PrivateMetadataBase privateMetadata) => privateMetadata.ToString();
}
