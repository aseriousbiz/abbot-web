namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents an argument that is a user mention.
/// </summary>
public interface IMentionArgument : IArgument
{
    /// <summary>
    /// The mentioned user.
    /// </summary>
    IChatUser Mentioned { get; }
}
