namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents one argument tokenized from the Bot.Arguments property with the original
/// potentially quoted value stored..
/// </summary>
public interface IOriginalArgument : IArgument
{
    /// <summary>
    /// The original value of the argument, which may include quotes if it was quoted.
    /// </summary>
    string OriginalText { get; }
}
