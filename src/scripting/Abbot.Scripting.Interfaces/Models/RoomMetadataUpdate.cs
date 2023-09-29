using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to update room metadata.
/// </summary>
/// <param name="Values">The metadata values. If any keys do not match defined existing metadata, they are ignored.</param>
public record RoomMetadataUpdate(IReadOnlyDictionary<string, string?> Values);
