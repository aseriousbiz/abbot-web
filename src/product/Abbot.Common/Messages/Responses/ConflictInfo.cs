using System;

namespace Serious.Abbot.Messages;

/// <summary>
/// Provides additional information about a conflict. This can be retrieved on the client by examining the
/// ProblemDetails.Extensions dictionary for the "Conflict" key.
/// </summary>
public class ConflictInfo
{
    /// <summary>
    /// The date the resource was last modified.
    /// </summary>
    public DateTime Modified { get; set; }

    /// <summary>
    /// The user that last modified the resource.
    /// </summary>
    public UserGetResponse ModifiedBy { get; set; } = null!;
}
