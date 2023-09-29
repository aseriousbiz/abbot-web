using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Settings.Rooms;
using Serious.Filters;

namespace Serious.Abbot.Pages.Tasks;

public record TaskItemModel(
    TaskItem Task,
    IList<int> TaskIds,
    FilterList Filter,
    AssigneeContainer AssigneeContainer)
{
    public static TaskItemModel FromTask(TaskItem task, IndexPageModel page) =>
        new(task, page.TaskIds, page.Filter, page.AssigneeContainer);

    public DomId DomId => Task.GetDomId();

    public string GetTaskFormId(string formType) => GetTaskFormId(Task, formType);

    public static string GetTaskFormId(Id<TaskItem> task, string formType) => $"task-{task}-form-{formType}";
}

