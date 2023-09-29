using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Filters;

namespace Serious.Abbot.Pages.Tasks;

public class EditPage : UserPage
{
    readonly TaskRepository _taskRepository;
    readonly CustomerRepository _customerRepository;
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;

    public EditPage(
        TaskRepository taskRepository,
        CustomerRepository customerRepository,
        IUserRepository userRepository,
        IRoleManager roleManager)
    {
        _taskRepository = taskRepository;
        _customerRepository = customerRepository;
        _userRepository = userRepository;
        _roleManager = roleManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new("", default, null, TaskItemStatus.None);

    [BindProperty(Name = "q", SupportsGet = true)]
    public FilterList Filter { get; set; } = new();

    public IReadOnlyList<SelectListItem> CustomerOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> AssigneeOptions { get; private set; } = Array.Empty<SelectListItem>();

    public async Task<IActionResult> OnGetAsync(Id<TaskItem> id)
    {
        if (!Viewer.CanManageConversations())
        {
            return RedirectToPage("/Settings/Account/Index");
        }
        if (!Organization.HasPlanFeature(PlanFeature.ConversationTracking))
        {
            StatusMessage = "You must upgrade your plan to use this feature.";
            return RedirectToPage();
        }

        if (await InitializePageAsync(id) is null)
        {
            return NotFound();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Id<TaskItem> id)
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

        if (!ModelState.IsValid)
        {
            await InitializePageAsync(id);
            return Page();
        }

        var assignee = Input.Assignee is not null
            ? await _userRepository.GetByPlatformUserIdAsync(Input.Assignee, Organization)
            : null;
        var customer = Input.CustomerId is not null
            ? await _customerRepository.GetByIdAsync(Input.CustomerId.Value, Organization)
            : null;

        await _taskRepository.UpdateTaskAsync(
            id,
            Input.Title,
            assignee,
            customer,
            Input.Status,
            Viewer,
            Organization);
        StatusMessage = "Task updated!";
        return RedirectToPage("/Tasks/Index", new { q = Filter.ToString() });
    }

    async Task<TaskItem?> InitializePageAsync(Id<TaskItem> id)
    {
        var task = await _taskRepository.GetByIdAsync(id, Organization);
        if (task is null)
        {
            return null;
        }

        Input = new InputModel(task.Title, task.Customer, task.Assignee?.User.PlatformUserId, task.Properties.Status);

        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
        AssigneeOptions = agents.Select(a => new SelectListItem(a.DisplayName, a.User.PlatformUserId)).ToList();
        var customers = await _customerRepository.GetAllAsync(Organization);
        var customerOptions = customers.Select(c => new SelectListItem(c.Name, c.Id.ToString(CultureInfo.InvariantCulture))).ToList();
        customerOptions.Insert(0, new SelectListItem("Unassigned", ""));
        CustomerOptions = customerOptions;

        return task;
    }

    public record InputModel(
        string Title,
        [property:Display(Name = "Customer")]
        Id<Customer>? CustomerId,
        string? Assignee,
        TaskItemStatus Status);
}
