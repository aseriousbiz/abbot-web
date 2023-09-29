namespace Serious.Abbot.Messages;

/// <summary>
/// Response to the /api/cli/auth endpoint.
/// </summary>
public class StatusGetResponse
{
    /// <summary>
    /// Information about the organization associated with an API Key.
    /// </summary>
    public OrganizationGetResponse Organization { get; set; } = null!;

    /// <summary>
    /// Information about owner of an API key.
    /// </summary>
    public UserGetResponse User { get; set; } = null!;
}
