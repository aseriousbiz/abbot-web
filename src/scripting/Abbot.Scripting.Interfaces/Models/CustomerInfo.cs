using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Information about a customer.
/// </summary>
public record CustomerInfo
{
    /// <summary>
    /// The database Id for the customer.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The name of the customer.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Information about a room.
    /// </summary>
    public required IReadOnlyList<IRoom> Rooms { get; init; }

    /// <summary>
    /// The set of tags applied to this room.
    /// </summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>
    /// Custom metadata associated with this customer.
    /// </summary>
    public required IReadOnlyDictionary<string, string?> Metadata { get; init; }
}
