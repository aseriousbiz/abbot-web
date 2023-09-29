namespace Serious.Abbot.Messages;

public class OrganizationGetResponse
{
    /// <summary>
    /// The name of the organization
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The name of the organization
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// The platform Id for the organization.
    /// </summary>
    public string PlatformId { get; set; } = string.Empty;

    /// <summary>
    /// The name of the platform the user is on.
    /// </summary>
    public string Platform { get; set; } = null!;
}
