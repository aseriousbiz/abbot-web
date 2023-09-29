using System;
using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Information about a task.
/// </summary>
public record TaskItemInfo : TaskRequest
{
    /// <summary>
    /// The database Id for the customer.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The user that created this task.
    /// </summary>
    public required IChatUser Creator { get; init; }

    /// <summary>
    /// The date this task was created.
    /// </summary>
    public required DateTime CreatedUtc { get; init; }

    /// <summary>
    /// The user that last modified this task.
    /// </summary>
    public required IChatUser ModifiedBy { get; init; }

    /// <summary>
    /// The date this task was last modified.
    /// </summary>
    public required DateTime Modified { get; init; }
}

/// <summary>
/// The status of a Task.
/// </summary>
public enum TaskItemStatus
{
    /// <summary>
    /// The task has no status.
    /// </summary>
    None,

    /// <summary>
    /// The task is opened.
    /// </summary>
    Open,

    /// <summary>
    /// The task is in-progress,
    /// </summary>
    [Display(Name = "In Progress")]
    InProgress,

    /// <summary>
    /// The task is blocked.
    /// </summary>
    Blocked,

    /// <summary>
    /// The task is closed.
    /// </summary>
    Closed,
}
