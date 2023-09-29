namespace Serious.Abbot.Messages;

/// <summary>
/// Information about a user that owns an API Key
/// </summary>
public class UserGetResponse
{
    /// <summary>
    /// The user's name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The user's Id on the chat platform.
    /// </summary>
    public string PlatformUserId { get; set; } = string.Empty;
}
