using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Segment;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Models;
using Serious.Abbot.Telemetry;
using Serious.Collections;
using Serious.Filters;

namespace Serious.Abbot.Repositories;

public class TaskRepository : OrganizationScopedRepository<TaskItem>
{
    readonly IAnalyticsClient _analyticsClient;
    readonly IClock _clock;

    public TaskRepository(
        AbbotContext db,
        IAuditLog auditLog,
        IAnalyticsClient analyticsClient,
        IClock clock)
        : base(db, auditLog)
    {
        _analyticsClient = analyticsClient;
        _clock = clock;
    }

    protected override DbSet<TaskItem> Entities => Db.Tasks;

    protected override IQueryable<TaskItem> GetEntitiesQueryable()
    {
        return Entities
            .Include(t => t.Customer)
            .Include(t => t.Conversation)
            .ThenInclude(c => c!.Room)
            .ThenInclude(r => r.Customer)
            .Include(t => t.Conversation!.StartedBy)
            .ThenInclude(m => m.User)
            .Include(t => t.Organization)
            .Include(t => t.Assignee)
            .ThenInclude(a => a!.User)
            .OrderByDescending(t => t.Created);
    }

    public async Task<IPaginatedList<TaskItem>> GetTasksAsync(
        FilterList filter,
        Organization organization,
        int pageNumber,
        int pageSize)
    {
        var query = GetQueryable(organization).ApplyFilter(filter);
        return await PaginatedList.CreateAsync(query, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<Member>> GetAllAssigneesAsync(Organization organization)
    {
        var assignees = await Db.Tasks
            .Include(t => t.Assignee)
            .ThenInclude(a => a!.User)
            .Where(t => t.OrganizationId == organization.Id)
            .Where(a => a.Assignee != null)
            .Select(t => t.Assignee!)
            .ToListAsync();

        return assignees.DistinctBy(a => a.Id).ToList();
    }

    public async Task<TaskItem> CreateTaskAsync(
        string title,
        Id<Member>? assignee,
        Id<Customer>? customer,
        Member actor,
        Organization organization)
    {
        var task = new TaskItem
        {
            Title = title,
            Properties = new TaskProperties
            {
                Status = TaskItemStatus.Open,
            },
            AssigneeId = assignee,
            Organization = organization,
            OrganizationId = organization.Id,
            CustomerId = customer,
        };

        var created = await CreateAsync(task, actor.User);

        await LogActivityAsync(new[] { created }, "create task page", "Created", actor, organization);
        return created;
    }

    public async Task<TaskItem?> UpdateTaskAsync(
        Id<TaskItem> id,
        string title,
        Id<Member>? assignee,
        Id<Customer>? customer,
        TaskItemStatus status,
        Member actor,
        Organization organization)
    {
        var task = await GetByIdAsync(id, organization);
        if (task is null)
        {
            return null;
        }

        task.Title = title;
        task.AssigneeId = assignee;
        task.CustomerId = customer;
        task.Properties = task.Properties with
        {
            Status = status
        };
        task.Closed = status is TaskItemStatus.Closed ? _clock.UtcNow : null;
        await UpdateAsync(task, actor.User);
        await LogActivityAsync(new[] { task }, "edit task page", "Updated", actor, organization);
        return task;
    }

    /// <summary>
    /// Updates the status of the given set of tasks.
    /// </summary>
    /// <param name="tasks">The set of tasks to close.</param>
    /// <param name="status">The status to update these tasks to.</param>
    /// <param name="actor">The person doing the assignment.</param>
    /// <param name="organization">The <see cref="Organization"/> where the task lives.</param>
    /// <returns>The number of tickets where the status was changed by this operation</returns>
    public async Task<int> UpdateStatusesAsync(
        IEnumerable<TaskItem> tasks,
        TaskItemStatus status,
        Member actor,
        Organization organization)
        => await BulkActionAsync(
            "Updated",
            tasks.Where(t => t.Properties.Status != status),
            "task list",
            t => {
                t.Closed = _clock.UtcNow;
                t.Properties = t.Properties with
                {
                    Status = status,
                };
                t.Closed = status is TaskItemStatus.Closed ? _clock.UtcNow : null;
            },
            actor,
            organization);

    /// <summary>
    /// Promotes the given set of conversations to tasks.
    /// </summary>
    /// <param name="conversations">The set of <see cref="Conversation"/>s to promote.</param>
    /// <param name="actor">The person doing the promoting.</param>
    /// <param name="organization">The organization that owns the resulting tasks.</param>
    /// <returns>The set of tasks created.</returns>
    public async Task<IReadOnlyList<TaskItem>> PromoteSuggestedTasksAsync(
        IEnumerable<Conversation> conversations,
        Member actor,
        Organization organization)
    {
        List<TaskItem> createdTasks = new List<TaskItem>();

        foreach (var conversation in conversations)
        {
            var task = new TaskItem
            {
                Title = conversation.Properties.Conclusion.Require(),
                Properties = new TaskProperties
                {
                    MessageId = conversation.FirstMessageId,
                    Status = TaskItemStatus.Open,
                },
                Assignee = conversation.Assignees.FirstOrDefault(),
                Organization = organization,
                OrganizationId = organization.Id,
                Conversation = conversation,
                Customer = conversation.Room.Customer,
            };
            var created = await CreateAsync(task, actor.User);
            createdTasks.Add(created);
            conversation.SerializedProperties = (conversation.Properties with
            {
                RelatedTaskItemId = created,
            }).ToJson();

            await Db.SaveChangesAsync();
        }

        await LogActivityAsync(createdTasks, "task list promotion", "Created", actor, organization);
        return createdTasks;
    }

    /// <summary>
    /// Assigns the set of tasks to the given assignee for the specified organization.
    /// </summary>
    /// <param name="tasks">The set of tasks to assign.</param>
    /// <param name="assignee">The person assigned to the tasks.</param>
    /// <param name="actor">The person doing the assignment.</param>
    /// <param name="organization">The <see cref="Organization"/> where the tasks live.</param>
    /// <returns>The number of tasks assigned by this operation</returns>
    public async Task<int> AssignTasksAsync(
        IEnumerable<TaskItem> tasks,
        Member assignee,
        Member actor,
        Organization organization)
        => await BulkActionAsync(
            "Assigned",
            tasks.Where(t => t.Properties.Status != TaskItemStatus.Closed),
            "task list",
            t => t.Assignee = assignee,
            actor,
            organization,
            count => $"Assigned {count.ToQuantity("task")} to {assignee.DisplayName}.");

    /// <summary>
    /// Closes the set of tasks.
    /// </summary>
    /// <param name="tasks">The set of tasks to close.</param>
    /// <param name="actor">The person doing the assignment.</param>
    /// <param name="organization">The <see cref="Organization"/> where the task lives.</param>
    /// <returns>The number of tickets closed by this operation</returns>
    public async Task<int> CloseTasksAsync(
        IEnumerable<TaskItem> tasks,
        Member actor,
        Organization organization)
        => await BulkActionAsync(
            "Closed",
            tasks.Where(t => t.Properties.Status != TaskItemStatus.Closed),
            "task list",
            t => {
                t.Closed = _clock.UtcNow;
                t.Properties = t.Properties with
                {
                    Status = TaskItemStatus.Closed,
                };
            },
            actor,
            organization);

    async Task<int> BulkActionAsync(
        string actionVerb,
        IEnumerable<TaskItem> tasks,
        string source,
        Action<TaskItem> action,
        Member actor,
        Organization organization,
        Func<int, string>? description = null)
    {
        int tasksActedOn = 0;
        var taskInfos = new List<TaskInfo>();
        foreach (var task in tasks)
        {
            Expect.True(task.OrganizationId == organization.Id, "Task org must match passed in org.");
            task.ModifiedBy = actor.User;
            action(task);
            taskInfos.Add(TaskInfo.FromTaskItem(task));
            tasksActedOn++;
        }

        if (tasksActedOn > 0)
        {
            await Db.SaveChangesAsync();
            await LogActivityAsync(taskInfos, source, actionVerb, actor, organization, description?.Invoke(tasksActedOn));
        }
        return tasksActedOn;
    }

    async Task LogActivityAsync(
        IEnumerable<TaskItem> tasks,
        string source,
        string verb,
        Member actor,
        Organization organization,
        string? description = null) =>
        await LogActivityAsync(
            tasks.Select(TaskInfo.FromTaskItem).ToList(),
            source,
             verb,
            actor,
            organization,
            description);

    async Task LogActivityAsync(
        IReadOnlyCollection<TaskInfo> tasks,
        string source,
        string verb,
        Member actor,
        Organization organization,
        string? description = null)
    {
        if (tasks.Count is 0)
        {
            return;
        }
        await AuditLog.LogAuditEventAsync(
            new()
            {
                Type = new("Task", verb),
                Actor = actor,
                Organization = organization,
                Description = description ?? $"{verb} {tasks.Count.ToQuantity("task")}.",
                Properties = new TaskEventProperties(tasks),
            });
        _analyticsClient.Track(
            $"Task {verb}",
            AnalyticsFeature.Tasks,
            actor,
            organization,
            new {
                count = tasks.Count,
                source,
            });
    }
}

public record TaskEventProperties(IReadOnlyCollection<TaskInfo> Tasks);

public record TaskInfo(
    Id<TaskItem> Id,
    string Title,
    TaskItemStatus Status,
    CustomerInfo? Customer,
    PlatformUser? Assignee,
    DateTime? Closed)
{
    public static TaskInfo FromTaskItem(TaskItem taskItem)
        => new(
            taskItem,
            taskItem.Title,
            taskItem.Properties.Status,
            taskItem.Customer?.ToCustomerInfo(),
            taskItem.Assignee?.ToPlatformUser(),
            taskItem.Closed);
}
