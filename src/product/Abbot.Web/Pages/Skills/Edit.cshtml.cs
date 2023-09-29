using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.PageServices;
using Serious.Abbot.Repositories;
using Serious.Abbot.Validation;

namespace Serious.Abbot.Pages.Skills;

public class EditPage : CustomSkillPageModel
{
    public DomId EditorFormId { get; } = new("editorForm");
    public DomId SidebarDomId { get; } = new("skill-sidebar");
    public DomId SkillUpgradeLinkDomId { get; } = new("skill-upgrade-link");
    public DomId SkillPublishPackageSection { get; } = new("skill-package-publish-section");
    public DomId SecretsSidebarHeaderDomId { get; } = new("skill-secrets-sidebar-header");
    public DomId SkillNavigationTabsDomId { get; } = new("skill-navigation-tabs");

    readonly ISkillEditorService _skillEditorService;
    readonly ISkillNameValidator _skillNameValidator;
    readonly IPermissionRepository _permissions;

    public EditPage(
        ISkillEditorService skillEditorService,
        ISkillNameValidator skillNameValidator,
        IPermissionRepository permissions)
    {
        _skillEditorService = skillEditorService;
        _skillNameValidator = skillNameValidator;
        _permissions = permissions;
    }

    public SkillEditPermissionModel Permissions { get; private set; } = null!;

    public bool CanEditSkill => Permissions.CanEditSkill;

    public int SecretCount { get; private set; }

    public bool HasPackage { get; private set; }

    /// <summary>
    ///  The published package for this skill.
    /// </summary>
    public PackageVersion? PackageVersion { get; private set; }

    public bool HasPackageUpdates { get; private set; }

    public bool HasUnpublishedChanges { get; private set; }

    public PackageVersion? LatestSourcePackageVersion { get; private set; }

    public PackageVersion? CurrentSourcePackageVersion { get; private set; }

    public CodeLanguage Language { get; private set; }

    public string OrganizationSlug { get; private set; } = null!;

    public Skill Skill { get; private set; } = null!;

    public bool HasSourcePackage { get; private set; }

    [BindProperty]
    public InputModel Input { get; init; } = new();

    public async Task<IActionResult> OnGetAsync(string skill)
    {
        var (skillExists, _) = await InitializeState(skill);

        if (!skillExists)
        {
            // This only happens if we have an id, but it doesn't match a skill.
            return NotFound();
        }

        Input.UpdateFormFromSkill(Skill);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string skill)
    {
        var (skillExists, (user, _)) = await InitializeState(skill);

        if (!skillExists)
        {
            return NotFound();
        }

        if (!Permissions.CanEditSkill)
        {
            return TurboFlash("Permission denied.", isError: true);
        }

        // Skill is set to dbSkill in InitializeState
        var validationResult = await _skillNameValidator.IsUniqueNameAsync(
            Input.Name,
            Skill.Id,
            nameof(Skill),
            Skill.Organization);

        if (!validationResult.IsUnique)
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Name)}", "The skill name is not unique.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var updateModel = Input.ToSkillUpdateModel(Skill, Permissions);

        // If this is JavaScript code, make sure the wrapper isn't persisted into the database.
        if (updateModel.Code is not null && Skill.Language is CodeLanguage.JavaScript)
        {
            var codeLines = updateModel.Code.Split("\n");
            // Skip the first and last lines of the skill code since they're templated.
            updateModel.Code = string.Join("\n", codeLines[1..^1]);
        }

        var result = await _skillEditorService.UpdateAsync(
            Skill,
            updateModel,
            user);

        if (result.Saved)
        {
            if (updateModel.Name is not null)
            {
                // Skill name changed, we need to update multiple sections of the page.
                return TurboStream(
                    TurboFlash("Skill Updated"),
                    TurboPageLocation(
                        new {
                            skill = Skill.Name
                        }),
                    TurboUpdate(SidebarDomId, "_SkillSidebar", this),
                    TurboUpdate(SkillNavigationTabsDomId, "_SkillNavigationTabs", Skill),
                    TurboUpdate(SkillUpgradeLinkDomId, "_SkillUpgradeLink", Skill),
                    TurboUpdate(SkillPublishPackageSection, "_SkillPublishPackageSection", this),
                    TurboUpdate(SecretsSidebarHeaderDomId, "_SecretsSidebarHeader", Skill),
                    TurboUpdateFormDefaults(EditorFormId));
            }

            return TurboStream(TurboFlash("Skill Updated."), TurboUpdateFormDefaults(EditorFormId));
        }

        return result.CompilationErrors is { Count: > 0 }
            ? TurboFlash($"Skill failed to compile: {result.CompilationErrors[0]}", isError: true) // Cheap and easy, we show the first error. Not the best dev experience, but we ain't JetBrains either.
            : TurboFlash("There were no changes to save.");
    }

    async Task<(bool, Member)> InitializeState(string skill)
    {
        var member = Viewer;
        var dbSkill = await _skillEditorService.GetAsync(skill, member.Organization); // This could return null

        if (dbSkill is null)
        {
            return (false, member);
        }

        Skill = dbSkill;

        var package = Skill.Package;
        if (package is not null)
        {
            HasPackage = true;
            HasUnpublishedChanges = Skill.HasUnpublishedChanges(package);
            PackageVersion = package.GetLatestVersion();
        }

        CurrentSourcePackageVersion = Skill.SourcePackageVersion;
        LatestSourcePackageVersion = Skill.SourcePackageVersion?.Package.GetLatestVersion();

        HasPackageUpdates = LatestSourcePackageVersion is not null
                            && CurrentSourcePackageVersion is not null
                            && LatestSourcePackageVersion.Id != CurrentSourcePackageVersion.Id;

        // Always wrap JS skills in a dummy async module for linting purposes.
        if (Skill.Language is CodeLanguage.JavaScript)
        {
            var skillName = string.Empty;
            if (!string.IsNullOrEmpty(Skill.Name))
            {
                skillName = "." + Skill.Name.ToPascalCase();
            }

            Skill.Code = $"module.exports{skillName} = (async () => {{ // We modularize your code in order to run it.\n{Skill.Code}\n}})(); // Thanks for building with Abbot!";
        }

        bool forceEdit = Request.Query["forceEdit"] == "true";

        HasSourcePackage = Skill.SourcePackageVersionId is not null;

        SecretCount = Skill.Secrets.Count;
        Language = Skill.Language;
        OrganizationSlug = member.Organization.Slug;

        // If member is in the Administrators role, then this returns Capability.Admin
        var capability = await _permissions.GetCapabilityAsync(member, Skill);
        Permissions = new SkillEditPermissionModel(
            Skill,
            capability,
            forceEdit,
            member.Organization.HasPlanFeature(PlanFeature.SkillPermissions));

        return (true, member);
    }

    public class InputModel
    {
        public int? Id { get; set; } // Used to validate unique skill name.

        [StringLength(38, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 38 characters in length.")]
        [RegularExpression(@"^[a-zA-Z0-9](?:[a-zA-Z0-9]|-(?=[a-zA-Z0-9])){0,38}$",
            ErrorMessage =
                "Name may only contain a-z and 0-9. For multi-word names, separate the words by a dash character.")]
        [Remote(action: "Validate",
            controller: "SkillValidation",
            areaName: "InternalApi",
            AdditionalFields = "Id, Type")]
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Display(Description =
            "Provide examples of usage. Simple markdown formatting allowed such as using backticks ` to surround the usage example and _ to surround the description of what the usage example does.")]
        public string? Usage { get; set; }

        public string? Arguments { get; set; }

        public string Type => nameof(Skill);

        public CodeLanguage Language { get; set; }

        public bool Restricted { get; set; }

        public bool Enabled { get; set; }

        public SkillDataScope Scope { get; set; }

        public void UpdateFormFromSkill(Skill skill)
        {
            Id = skill.Id;
            Name = skill.Name;
            Code = skill.Code;
            Description = skill.Description;
            Usage = skill.UsageText;
            Language = skill.Language;
            Restricted = skill.Restricted;
            Enabled = skill.Enabled;
            Scope = skill.Scope;
        }

        public SkillUpdateModel ToSkillUpdateModel(Skill skill, SkillEditPermissionModel permissions)
        {
            bool restricted = permissions.CanChangeRestricted
                ? Restricted
                : skill.Restricted;

            return new()
            {
                Name = Name,
                Code = Code,
                Description = Description,
                UsageText = Usage,
                Restricted = restricted,
                Enabled = Enabled,
                Scope = Scope,
            };
        }
    }
}
