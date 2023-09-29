using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.Abbot.Entities;
using Serious.Abbot.Entities.Filters;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Settings.Rooms;
using Serious.Abbot.Pages.Shared.Filters;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.AspNetCore.Turbo;
using Serious.Collections;
using Serious.Filters;

namespace Serious.Abbot.Pages.Tasks;

public class IndexPageModel : UserPage
{
    readonly TaskRepository _taskRepository;
    readonly IConversationRepository _conversationRepository;
    readonly IUserRepository _userRepository;
    readonly CustomerRepository _customerRepository;
    readonly IRoleManager _roleManager;
    readonly IClock _clock;

    public IndexPageModel(
        TaskRepository taskRepository,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        CustomerRepository customerRepository,
        IRoleManager roleManager,
        IClock clock)
    {
        _taskRepository = taskRepository;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _customerRepository = customerRepository;
        _roleManager = roleManager;
        _clock = clock;
    }

    public IReadOnlyList<SelectListItem> Filters { get; private set; } = Array.Empty<SelectListItem>();

    [BindProperty(Name = "q", SupportsGet = true)]
    public FilterList Filter { get; set; } = new();

    /// <summary>
    /// For now, we associate the thread with the conversation that originated it. This will probably change later.
    /// </summary>
    [BindProperty]
    public IList<string> ThreadIds { get; set; } = Array.Empty<string>();

    [BindProperty]
    public IList<int> TaskIds { get; set; } = Array.Empty<int>();

    public IPaginatedList<TaskItem> Tasks { get; private set; } = null!;

    [BindProperty]
    public string? Assignee { get; set; }

    [BindProperty]
    public TaskItemStatus NewStatus { get; set; }

    public CompositeFilterModel CustomerFilterModel { get; private set; } = null!;

    public FilterModel AssigneeFilterModel { get; private set; } = null!;

    public FilterModel StatusFilterModel { get; private set; } = null!;

    public AssigneeContainer AssigneeContainer { get; private set; } = null!;

    public RespondersContainer DefaultFirstResponders { get; private set; } = null!;

    public RespondersContainer DefaultEscalationResponders { get; private set; } = null!;

    public string Tab { get; init; } = "";

    [BindProperty(Name = "p", SupportsGet = true)]
    public int? PageNumber { get; init; }

    public int PageSize { get; init; }

    public IPaginatedList<Conversation> SuggestedTasks { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string? source, int? sp)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }

        if (PageNumber < 1)
        {
            return RedirectToPage(1);
        }

        var assignees = await _taskRepository.GetAllAssigneesAsync(Organization);
        var assigneeOptions = new[] { new FilterOption("Unassigned", "none") }
            .Concat(assignees.Select(AbbotFilterModelHelpers.CreateOptionFromMember));

        AssigneeFilterModel = FilterModel.Create(assigneeOptions, Filter, "assignee", "Assignee");

        Tasks = await _taskRepository.GetTasksAsync(
            Filter,
            Organization,
            PageNumber ?? 1,
            WebConstants.ShortPageSize);

        if (PageNumber > 1 && Tasks.Count is 0)
        {
            return RedirectToPage(1);
        }

        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
        AssigneeContainer = new AssigneeContainer(agents, null, Organization);
        DefaultFirstResponders = await GetDefaultResponders(RoomRole.FirstResponder, agents);
        DefaultEscalationResponders = await GetDefaultResponders(RoomRole.EscalationResponder, agents);
        var customers = await _customerRepository.GetAllAsync(Organization);
        var segments = await _customerRepository.GetAllCustomerSegmentsAsync(Organization);
        CustomerFilterModel = AbbotFilterModelHelpers.CreateCustomerFilterModel(
            customers,
            segments,
            Filter);

        StatusFilterModel = FilterModel.Create(Enum.GetValues<TaskItemStatus>(), Filter, "is", "Status", s => $"{s}");

        Filters = GenerateFiltersList(source, Filter).ToList();

        var query = new ConversationQuery(Organization)
            .WithState(ConversationStateFilter.NeedsResponse)
            .WithSuggestedTask();

        if (source is "my")
        {
            query = query.InRoomsWhereResponder(Viewer);
        }

        var page = sp ?? 1;
        var conversations = await _conversationRepository.QueryConversationsAsync(
            query,
            _clock.UtcNow,
            page,
            WebConstants.LongPageSize);

        conversations.PageQueryStringParameterName = "sp";

        SuggestedTasks = conversations;

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAssigneeAsync()
    {
        if (!Viewer.CanManageConversations())
        {
            return Forbid();
        }

        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        var results = await _taskRepository.GetAllByIdsAsync(
            TaskIds.Select(id => new Id<TaskItem>(id)),
            Organization);

        if (Assignee is null)
        {
            StatusMessage = "Please select an assignee.";
            return RedirectToPage();
        }

        var assignee = await _userRepository.GetByPlatformUserIdAsync(Assignee, Organization);
        if (assignee is null)
        {
            StatusMessage = "Please select a valid assignee.";
            return RedirectToPage();
        }

        var tasksToAssign = results.Select(r => r.Entity).WhereNotNull().ToList();
        var assignmentsUpdated = await _taskRepository.AssignTasksAsync(
            tasksToAssign,
            assignee,
            Viewer,
            Organization);

        return await TurboUpdateTasks(
            tasksToAssign,
            $"{assignmentsUpdated.ToQuantity("Task assignment")} updated!");
    }

    public async Task<IActionResult> OnPostChangeStatusesAsync()
    {
        if (!Viewer.CanManageConversations())
        {
            return Forbid();
        }

        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        var results = await _taskRepository.GetAllByIdsAsync(
            TaskIds.Select(id => new Id<TaskItem>(id)),
            Organization);

        var tasksToModify = results.Select(r => r.Entity).WhereNotNull().ToList();
        var tasksModified = await _taskRepository.UpdateStatusesAsync(tasksToModify, NewStatus, Viewer, Organization);

        return await TurboUpdateTasks(
            tasksToModify,
            $"Changed status for {tasksModified.ToQuantity("Task")} to {NewStatus}!");
    }

    public async Task<IActionResult> OnPostCloseTasksAsync()
    {
        if (!Viewer.CanManageConversations())
        {
            return Forbid();
        }

        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        var results = await _taskRepository.GetAllByIdsAsync(
            TaskIds.Select(id => new Id<TaskItem>(id)),
            Organization);

        var tasksToClose = results.Select(r => r.Entity).WhereNotNull().ToList();
        var tasksClosed = await _taskRepository.CloseTasksAsync(tasksToClose, Viewer, Organization);

        return await TurboUpdateTasks(tasksToClose, $"{tasksClosed.ToQuantity("Task")} closed!");
    }

    public async Task<IActionResult> OnPostCreateTasksAsync()
    {
        if (!Viewer.CanManageConversations())
        {
            return Forbid();
        }

        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        var results = await _conversationRepository.GetConversationsByThreadIdsAsync(
            ThreadIds,
            Organization);

        var conversationToPromote = results.Select(r => r.Entity).WhereNotNull();
        var createdTasks = await _taskRepository.PromoteSuggestedTasksAsync(
            conversationToPromote,
            Viewer,
            Organization);

        StatusMessage = $"{createdTasks.Count.ToQuantity("Task")} created!";
        return RedirectToPage();
    }

    public async Task InitializeAsync()
    {
        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
        AssigneeContainer = new AssigneeContainer(agents, null, Organization);
    }

    async Task<RespondersContainer> GetDefaultResponders(RoomRole roomRole, IReadOnlyList<Member> agents)
    {
        var (members, description) = roomRole switch
        {
            RoomRole.FirstResponder => (await _userRepository.GetDefaultFirstRespondersAsync(Organization),
                "a first responder for this organization"),
            RoomRole.EscalationResponder => (await _userRepository.GetDefaultEscalationRespondersAsync(Organization),
                "an escalation responder for this organization"),
            _ => throw new UnreachableException($"{roomRole}"),
        };

        return new RespondersContainer(
            members,
            agents,
            Viewer,
            Organization,
            roomRole,
            description);
    }

    public RedirectToPageResult RedirectToPage(int pageNumber)
    {
        return RedirectToPage(new {
            p = pageNumber,
            q = Filter.ToString(),
        });
    }

    public override RedirectToPageResult RedirectToPage()
    {
        return RedirectToPage(PageNumber);
    }

    public async Task<TurboStreamViewResult> TurboUpdateTasks(IEnumerable<TaskItem> updatedTasks, string statusMessage)
    {
        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
        var assigneeContainer = new AssigneeContainer(agents, null, Organization);

        var turboUpdates = updatedTasks
            .Select(t => new TaskItemModel(t, TaskIds, Filter, assigneeContainer))
            .Select(m => TurboUpdate(m.DomId, "_TaskListItem", m))
            .Append(TurboFlash(statusMessage))
            .ToArray();
        return TurboStream(turboUpdates);
    }

    IEnumerable<SelectListItem> GenerateFiltersList(string? tasks, FilterList filter)
    {
        yield return new SelectListItem(
            "All Conversations",
            Url.Page("/Tasks/Index",
                new {
                    source = (string?)null,
                    q = filter.ToString()
                }),
            tasks is null);

        yield return new SelectListItem(
            "Conversations in My Rooms",
            Url.Page("/Tasks/Index",
                new {
                    source = "my",
                    q = filter.ToString()
                }),
            tasks is "my");
    }
}
