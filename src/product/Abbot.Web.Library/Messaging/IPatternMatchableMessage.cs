namespace Serious.Abbot.Messaging;

/// <summary>
/// A message that can be pattern matched. This is a way for us to make sure
/// only the properties we want to match on are exposed to pattern matching.
/// </summary>
public interface IPatternMatchableMessage
{
    /// <summary>
    /// The incoming message text.
    /// </summary>
    string Text { get; }
}
