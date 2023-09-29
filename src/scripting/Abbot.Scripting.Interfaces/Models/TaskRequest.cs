namespace Serious.Abbot.Scripting;

/// <summary>
/// Used to create or update a task.
/// </summary>
public record TaskRequest
{
    /// <summary>
    /// The title of the task.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The person that this task is assigned to.
    /// </summary>
    public IChatUser? Assignee { get; init; }

    /// <summary>
    /// The status of the task.
    /// </summary>
    public TaskItemStatus Status { get; init; } = TaskItemStatus.Open;

    /// <summary>
    /// The customer this task is for.
    /// </summary>
    public CustomerInfo? Customer { get; init; }

    /// <summary>
    /// The conversation this task was created from.
    /// </summary>
    public ChatConversationInfo? Conversation { get; init; }
}
