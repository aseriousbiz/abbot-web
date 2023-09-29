namespace Serious.Abbot.Scripting;

/// <summary>
/// For internal use only.
/// </summary>
public enum ChatAddressType
{
    /// <summary>
    /// The <see cref="ChatAddress"/> refers to a user (for which a DM conversation must be created).
    /// </summary>
    User,
    /// <summary>
    /// The <see cref="ChatAddress"/> refers to a room ID.
    /// </summary>
    Room,
}
