using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Serious.Collections;

namespace Serious.Abbot.Pages.Skills;

public class IndexPage : SkillFeatureEditPageModel
{
    public DomId SkillListDomId => new("skill-list");

    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;

    public IndexPage(
        ISkillRepository skillRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _permissions = permissions;
        ReturnNotFoundIfCustomSkillsDisabled = false;
    }

    public override string? StaffPageUrl() =>
        Url.Page("/Staff/Organizations/Skills", new { Id = Organization.PlatformId });

    public IPaginatedList<Skill> Skills { get; private set; } = null!;

    public int? NumSkillsAllowed { get; private set; }

    public int? NumSkillsEnabled { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? skill)
    {
        if (skill is not null)
        {
            return RedirectToPage("Edit", new { skill });
        }

        await InitializePageAsync(WebConstants.LongPageSize);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentMember = Viewer;
        var (user, organization) = currentMember;
        var skill = await _skillRepository.GetAsync(Input.Name, organization);
        if (skill is null)
        {
            return NotFound();
        }

        if (!await _permissions.CanEditAsync(currentMember, skill))
        {
            StatusMessage = $"{WebConstants.ErrorStatusPrefix}Edit permission required to change this skill";
        }
        else
        {
            await _skillRepository.ToggleEnabledAsync(skill, Input.Enabled, user);
            var enabledMessage = Input.Enabled
                ? "enabled!"
                : "disabled.";
            StatusMessage = $"Skill {skill.Name} is now {enabledMessage}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostFilterAsync()
    {
        await InitializePageAsync(WebConstants.LongPageSize);
        return TurboUpdate(SkillListDomId, "Shared/_SkillList", this);
    }

    async Task InitializePageAsync(int pageSize)
    {
        int pageNumber = PageNumber ?? 1;

        var currentUser = Viewer;
        IQueryable<Skill> skillsQueryable = _skillRepository.GetSkillListQueryable(currentUser.Organization)
            .Include(s => s.Patterns)
            .Include(s => s.SignalSubscriptions)
            .Include(s => s.Exemplars);
        NumSkillsEnabled = skillsQueryable.Count(s => s.Enabled);
        skillsQueryable = FilterSkills(skillsQueryable, Filter)
            .OrderBy(s => s.Name);
        Skills = await PaginatedList.CreateAsync(skillsQueryable, pageNumber, pageSize);
        NumSkillsAllowed = currentUser.Organization.GetPlan().MaximumSkillCount;
    }

    public class InputModel
    {
        public string Name { get; set; } = null!;
        public bool Enabled { get; set; }
        public string? Filter { get; set; }
    }

    static IQueryable<Skill> FilterSkills(IQueryable<Skill> queryable, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return queryable;
        }
        if (Enum.TryParse<CodeLanguage>(filter, out var filterLanguage))
        {
            return queryable.Where(s => s.Language == filterLanguage);
        }

        if (filter is { Length: > 0 } filterString)
        {
            queryable = queryable
                .Where(p => p.Name.ToLower().Contains(filterString.ToLower())
                    || p.Description.ToLower().Contains(filterString.ToLower())
                    || p.Creator.DisplayName.ToLower().Contains(filterString.ToLower()));
        }

        return queryable;
    }
}
