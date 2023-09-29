namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents an argument that can be parsed into an integer.
/// </summary>
public interface IInt32Argument : IArgument
{
    /// <summary>
    /// The integer value of the argument.
    /// </summary>
    int Int32Value { get; }
}
