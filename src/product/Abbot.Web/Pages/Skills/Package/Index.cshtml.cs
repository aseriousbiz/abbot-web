using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Skills.Package;

public class IndexModel : CustomSkillPageModel
{
    readonly ISkillRepository _skillRepository;
    readonly IPackageRepository _packageRepository;
    readonly IPermissionRepository _permissions;

    public IndexModel(
        ISkillRepository skillRepository,
        IPackageRepository packageRepository,
        IPermissionRepository permissions)
    {
        _skillRepository = skillRepository;
        _packageRepository = packageRepository;
        _permissions = permissions;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool PackageExists { get; private set; }

    public bool ChangesExist { get; private set; }

    public string SkillName { get; private set; } = null!;

    public bool UsageTextChanged { get; private set; }
    public bool DescriptionChanged { get; private set; }
    public bool CodeChanged { get; private set; }
    public string UpdatedUsageText { get; private set; } = string.Empty;
    public string UpdatedDescription { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var (_, dbSkill, package) = await InitializePageAsync(skill);
        if (dbSkill is null)
        {
            return NotFound();
        }

        if (package is null)
        {
            Input.Readme = dbSkill.Secrets.Count > 0
                ? "This skill requires the following secrets to be configured:\n\n" + string.Join("\n", dbSkill.Secrets.Select(s => $"* `{s.Name}` {s.Description}"))
                : string.Empty;
        }
        else
        {
            Input.Readme = package.Readme;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill)
    {
        var (user, dbSkill, package) = await InitializePageAsync(skill);
        if (dbSkill is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (package is not null && !AnyChanges(package, dbSkill))
        {
            ModelState.AddModelError(string.Empty, "Publish failed. There are no changes to publish. To publish a new version, update the Readme, Description, Usage, or Code.");
            return Page();
        }

        string version;
        if (package is null)
        {
            await _packageRepository.CreateAsync(Input, dbSkill, user);
            version = "1.0.0";
        }
        else
        {
            var newVersion = await _packageRepository.PublishNewVersionAsync(Input, package, dbSkill, user);
            version = newVersion.ToVersionString();
        }
        StatusMessage = $"Package version {version} for the skill {skill} published successfully!";

        return RedirectToPage("../Edit", new { skill });
    }

    bool AnyChanges(Entities.Package package, Skill skill)
    {
        return !package.Description.Equals(skill.Description, StringComparison.Ordinal)
               || !package.Code.Equals(skill.Code, StringComparison.Ordinal)
               || !package.Readme.Equals(Input.Readme ?? string.Empty, StringComparison.Ordinal)
               || !package.UsageText.Equals(skill.UsageText, StringComparison.Ordinal);
    }

    async Task<(User, Skill?, Entities.Package?)> InitializePageAsync(string skill)
    {
        var member = Viewer;
        var (user, organization) = member;
        var dbSkill = await _skillRepository.GetAsync(skill, organization);
        if (dbSkill is null)
        {
            return (user, null, null);
        }

        if (!await _permissions.CanEditAsync(member, dbSkill))
        {
            return (user, null, null);
        }

        Input.ChangeType = ChangeType.Patch.ToString();
        SkillName = dbSkill.Name;
        var package = await _packageRepository.GetAsync(dbSkill);
        PackageExists = package is not null;

        CodeChanged = package is null || !package.Code.Equals(dbSkill.Code, StringComparison.Ordinal);
        UsageTextChanged = package is null || !package.UsageText.Equals(dbSkill.UsageText, StringComparison.Ordinal);
        DescriptionChanged = package is null || !package.Description.Equals(dbSkill.Description, StringComparison.Ordinal);
        ChangesExist = CodeChanged || UsageTextChanged || DescriptionChanged;

        UpdatedUsageText = dbSkill.UsageText;
        UpdatedDescription = dbSkill.Description;

        return (user, dbSkill, package);
    }

    public class InputModel : PackageUpdateModel
    {
    }
}
