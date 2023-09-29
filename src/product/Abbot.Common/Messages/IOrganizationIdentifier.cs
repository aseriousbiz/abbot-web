using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// The unique identity for a team or org on a chat platform according to the platform. This includes the platform
/// type and the team/org id on the platform. For example, Slack, T0123456789.
/// </summary>
public interface IOrganizationIdentifier
{
    /// <summary>
    /// The team or org id on the chat platform. For Slack this typically starts with "T" such as "T0123456789".
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// The platform type of the skill.
    /// </summary>
    PlatformType PlatformType => PlatformType.Slack;
}
