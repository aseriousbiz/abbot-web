using System;
using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to create or update a customer.
/// </summary>
public record CustomerRequest
{
    /// <summary>
    /// The name of the customer.
    /// </summary>
    public string Name { get; init; } = null!;

    /// <summary>
    /// The channels to assign to this customer.
    /// </summary>
    public IReadOnlyList<string> Rooms { get; init; } = Array.Empty<string>();

    /// <summary>
    /// THIS IS OBSOLETE. Use <see cref="Segments"/> instead!
    /// </summary>
    [Obsolete("Use Segments instead.")]
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The set of customer segments to associate with this customer. If any do not exist, they'll be created.
    /// </summary>
    public IReadOnlyList<string> Segments { get; init; } = Array.Empty<string>();
}
