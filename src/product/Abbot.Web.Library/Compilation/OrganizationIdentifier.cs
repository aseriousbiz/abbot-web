using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Cache;

/// <summary>
/// The unique identity for a team or org on a chat platform according to the platform. This includes the platform
/// type and the team/org id on the platform. For example, Slack, T0123456789.
/// </summary>
public sealed class OrganizationIdentifier : IOrganizationIdentifier
{
    public OrganizationIdentifier(string platformId, PlatformType platformType)
    {
        PlatformId = platformId;
        PlatformType = platformType;
    }

    /// <summary>
    /// The team or org id on the chat platform. For Slack this typically starts with "T" such as "T0123456789".
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// The platform type of the skill such as Slack or Teams.
    /// </summary>
    public PlatformType PlatformType { get; }
}
