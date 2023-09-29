using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Serialization;
using Serious.Filters;

namespace Serious.Abbot.Entities;

/// <summary>
/// A task that needs to be completed.
/// </summary>
public class TaskItem : TrackedEntityBase<TaskItem>, IOrganizationEntity, IFilterableEntity<TaskItem>
{
    /// <summary>
    /// The Id of the <see cref="Entities.Conversation"/> this task is associated with.
    /// </summary>
    public int? ConversationId { get; set; }

    /// <summary>
    /// The title of the task.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// The <see cref="Entities.Conversation"/> this task is associated with.
    /// </summary>
    public Conversation? Conversation { get; set; }

    /// <summary>
    /// The <see cref="Entities.Customer"/> this task is associated with.
    /// </summary>
    public Customer? Customer { get; set; }

    /// <summary>
    /// The Id of the <see cref="Entities.Customer"/> this task is associated with.
    /// </summary>
    public int? CustomerId { get; set; }

    /// <summary>
    /// The properties of this task. We put everything in here until we know what we need. We can then promote
    /// properties to full-fledged columns.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public TaskProperties Properties { get; set; } = new();

    /// <summary>
    /// The <see cref="Member"/> that this task is assigned to.
    /// </summary>
    public Member? Assignee { get; set; }

    /// <summary>
    /// The Id of the <see cref="Member"/> this task is assigned to.
    /// </summary>
    public int? AssigneeId { get; set; }

    /// <summary>
    /// The Id of the <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public required int OrganizationId { get; set; }

    /// <summary>
    /// The <see cref="Organization"/> this entity belongs to.
    /// </summary>
    public required Organization Organization { get; set; } = null!;

    /// <summary>
    /// The UTC timestamp the task was closed.
    /// </summary>
    public DateTime? Closed { get; set; }

    /// <summary>
    /// Returns all the <see cref="IFilterItemQuery{T}"/> that can be used to filter queries of T.
    /// </summary>
    /// <returns>The set of filter queries for this type.</returns>
    public static IEnumerable<IFilterItemQuery<TaskItem>> GetFilterItemQueries() => TaskItemFilters.CreateFilters();
}

/// <summary>
/// Grab bag of properties for a <see cref="Task"/>.
/// </summary>
public record TaskProperties : JsonSettings
{
    /// <summary>
    /// The Id of the specific message the task was extracted from.
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// The status of the task.
    /// </summary>
    public TaskItemStatus Status { get; init; } = TaskItemStatus.Open;
}

public static class TaskItemExtensions
{
    public static TaskItemInfo ToTaskItemInfo(this TaskItem task) =>
        new()
        {
            Id = task.Id,
            Title = task.Title,
            Assignee = task.Assignee?.ToPlatformUser(),
            Status = task.Properties.Status,
            Customer = task.Customer?.ToCustomerInfo(),
            Conversation = task.Conversation?.ToChatConversationInfo(),
            Creator = task.Creator.ToPlatformUser(),
            CreatedUtc = task.Created,
            ModifiedBy = task.ModifiedBy.ToPlatformUser(),
            Modified = task.Modified,
        };
}
