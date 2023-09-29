
using System;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents a threshold with a warning and critical level
/// </summary>
/// <param name="Warning">If the value goes above this level, the resource should be set to a Warning state.</param>
/// <param name="Deadline">If the value goes above this level, the resource should be set to an Overdue state.</param>
/// <typeparam name="T">The type of the value for the threshold.</typeparam>
public record Threshold<T>(T? Warning, T? Deadline) where T : struct, IComparable<T>;
