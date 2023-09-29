namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents one argument tokenized from the Bot.Arguments property.
/// </summary>
public interface IArgument
{
    /// <summary>
    /// The value of the argument sans quotes.
    /// </summary>
    string Value { get; }
}
