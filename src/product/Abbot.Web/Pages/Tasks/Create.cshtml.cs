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

public class CreatePage : UserPage
{
    readonly TaskRepository _taskRepository;
    readonly CustomerRepository _customerRepository;
    readonly IRoleManager _roleManager;

    public CreatePage(TaskRepository taskRepository, CustomerRepository customerRepository, IRoleManager roleManager)
    {
        _taskRepository = taskRepository;
        _customerRepository = customerRepository;
        _roleManager = roleManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new("", default, null);

    [BindProperty(Name = "q", SupportsGet = true)]
    public FilterList Filter { get; set; } = new();

    public IReadOnlyList<SelectListItem> CustomerOptions { get; private set; } = Array.Empty<SelectListItem>();

    public IReadOnlyList<SelectListItem> AssigneeOptions { get; private set; } = Array.Empty<SelectListItem>();

    public async Task<IActionResult> OnGetAsync()
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

        await InitializePageAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
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
            await InitializePageAsync();
            return Page();
        }

        await _taskRepository.CreateTaskAsync(Input.Title, null, null, Viewer, Organization);

        StatusMessage = "Task created!";
        return RedirectToPage("/Tasks/Index", new { q = Filter.ToString() });
    }

    async Task InitializePageAsync()
    {
        var agents = await _roleManager.GetMembersInRoleAsync(Roles.Agent, Organization);
        AssigneeOptions = agents.Select(a => new SelectListItem(a.DisplayName, a.User.PlatformUserId)).ToList();

        var customers = await _customerRepository.GetAllAsync(Organization);
        var customerOptions = customers
            .Select(c => new SelectListItem(c.Name, c.Id.ToString(CultureInfo.InvariantCulture), Filter["customer"]?.Value == c.Name))
            .ToList();
        customerOptions.Insert(0, new SelectListItem("Unassigned", ""));
        CustomerOptions = customerOptions;
    }

    public record InputModel(
        string Title,
        [property:Display(Name = "Customer")]
        Id<Customer>? CustomerId,
        string? Assignee);
}
