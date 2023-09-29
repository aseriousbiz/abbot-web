using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Abbot.Validation;
using Serious.AspNetCore;

namespace Serious.Abbot.Pages.Lists;

/// <summary>
/// Create used to edit lists. We do not allow creating lists from the web.
/// </summary>
public class EditPage : UserPage
{
    readonly IListRepository _userListRepository;
    readonly ISkillNameValidator _skillNameValidator;

    public EditPage(
        IListRepository userListRepository,
        ISkillNameValidator skillNameValidator)
    {
        _userListRepository = userListRepository;
        _skillNameValidator = skillNameValidator;
    }

    public string Name { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new InputModel();

    public UserList List { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(string name)
    {
        var (list, _) = await InitializeState(name);

        if (list is null)
        {
            // This only happens if we have an id, but it doesn't match a skill.
            return NotFound();
        }

        UpdateFormFromList(list);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string name)
    {
        var (list, user) = await InitializeState(name);

        if (list is null)
        {
            return NotFound();
        }

        if (list.Name != Input.Name)
        {
            // If the name changed, verify it's still unique.
            // Doing this only when the name changes is _not_ just a performance optimization.
            // If we do it when the name hasn't changed, we'll collide with the original list itself.
            var result =
                await _skillNameValidator.IsUniqueNameAsync(Input.Name, Input.Id, Input.Type, list.Organization);
            if (!result.IsUnique)
            {
                ModelState.AddModelError("Input.Name", "The list name is not unique.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        UpdateListFromForm(list);

        await _userListRepository.UpdateAsync(
            list,
            user);
        StatusMessage = "List updated!";

        bool needsRedirect = name != list.Name;

        return (Request.IsAjaxRequest(), needsRedirect) switch
        {
            (false, _) => RedirectToPage(new { name = list.Name }),
            (true, false) => Partial("_StatusMessage", StatusMessage),
            (true, true) => new JsonResult(new { redirect = true, url = Url.Page("Edit", new { name = list.Name }) })
        };
    }

    void UpdateListFromForm(UserList list)
    {
        list.Name = Input.Name;
        list.Description = Input.Description ?? string.Empty;
    }

    void UpdateFormFromList(UserList list)
    {
        Input.Id = list.Id;
        Input.Name = list.Name;
        Input.Description = list.Description;
    }

    async Task<(UserList?, User)> InitializeState(string name)
    {
        var user = Viewer;
        Name = name;
        var list = await _userListRepository.GetAsync(name, user.Organization);

        if (list is null)
        {
            return (null, user.User);
        }

        List = list;

        return (list, user.User);
    }

    public class InputModel
    {
        public int Id { get; set; } // Used to determine name uniqueness.
        [StringLength(38, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 38 characters in length.")]
        [RegularExpression(@"^[a-zA-Z0-9](?:[a-zA-Z0-9]|-(?=[a-zA-Z0-9])){0,38}$",
            ErrorMessage = "Name may only contain a-z and 0-9. For multi-word names, separate the words by a dash character.")]
        [Remote(action: "Get", controller: "SkillValidation", areaName: "InternalApi", AdditionalFields = "Id, Type")]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; } = nameof(UserList);
    }
}
