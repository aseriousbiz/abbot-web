using System;

namespace Serious.Abbot.Security;

/// <summary>
/// Values for the "RegistrationStatus" claim for specific registration states.
/// </summary>
public enum RegistrationStatus
{
    /// <summary>
    /// No special action needed.
    /// </summary>
    Ok,

    /// <summary>
    /// When an organization does not auto approve users, this is the state where the user needs to be approved
    /// to be added to the Members role.
    /// </summary>
    ApprovalRequired,
}
