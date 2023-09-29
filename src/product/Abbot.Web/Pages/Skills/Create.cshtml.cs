using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Models;
using Serious.Abbot.PageServices;
using Serious.Abbot.Repositories;
using Serious.Abbot.Validation;

namespace Serious.Abbot.Pages.Skills;

public class CreatePage : CustomSkillPageModel
{
    const string CSharpExample = "// Change the code below with the code for your skill! \nawait Bot.ReplyAsync(\"Hello \" + Bot.Arguments);";
    const string PythonExample = "# Change the code below with the code for your skill! \nbot.reply(\"Hello \" + bot.arguments)";
    const string JavaScriptExample = "  // Change the code below with the code for your skill! \nawait bot.reply(\"Hello \" + bot.arguments);";
    const string InkTemplate = "ink-template1.txt";

    readonly ISkillEditorService _skillEditorService;
    readonly IPackageRepository _packageRepository;
    readonly ISkillNameValidator _skillNameValidator;

    public CreatePage(
        ISkillEditorService skillEditorService,
        IPackageRepository packageRepository,
        ISkillNameValidator skillNameValidator)
    {
        _skillEditorService = skillEditorService;
        _packageRepository = packageRepository;
        _skillNameValidator = skillNameValidator;
    }

    public CodeLanguage Language { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// The source package if user is creating a new skill via a package.
    /// </summary>
    public PackageItemViewModel? PackageBeingInstalled { get; private set; }

    /// <summary>
    /// If installing a package, this is the package code.
    /// </summary>
    public string? Code { get; private set; }

    public int? SourcePackageVersionId => PackageBeingInstalled?.LatestVersion.Id;

    public async Task<IActionResult> OnGetAsync(string? language, int? fromPackageId)
    {
        if (!await InitializePage(language, fromPackageId))
        {
            return NotFound();
        }

        if (PackageBeingInstalled is not null)
        {
            Input.Name = PackageBeingInstalled.Name;
            Input.Description = PackageBeingInstalled.Description;
            Input.Usage = PackageBeingInstalled.UsageText;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? language, int? fromPackageId)
    {
        if (!await InitializePage(language, fromPackageId))
        {
            return NotFound();
        }

        var (user, organization) = Viewer;

        var result = await _skillNameValidator.IsUniqueNameAsync(Input.Name, 0, Input.Type, organization);
        if (!result.IsUnique)
        {
            // This will only happen if client validation fails for some reason. So we're
            // OK with this not being as detailed. Client validation provides more detailed
            // error messages.
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Name)}", "The skill name is not unique.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var code = PackageBeingInstalled is not null
            ? PackageBeingInstalled.Code
            : Language switch
            {
                CodeLanguage.CSharp => CSharpExample,
                CodeLanguage.Python => PythonExample,
                CodeLanguage.JavaScript => JavaScriptExample,
                CodeLanguage.Ink => await GetType().Assembly.ReadResourceAsync("Serious.Abbot", "Templates", InkTemplate),
                _ => CSharpExample
            };

        var updateModel = GetSkillCreateModelFromForm(code);

        var createResult = await _skillEditorService.CreateAsync(Language, updateModel, user, organization);
        if (createResult.CompiledSkill is not { } skill)
        {
            // Cheap and easy, we show the first error. Not the best dev experience, but we ain't JetBrains either.
            StatusMessage = $"Skill failed to compile: {createResult.CompilationErrors[0]}";
            return Page();
        }
        StatusMessage = "Skill created!";
        return RedirectToPage("Edit", new { skill = skill.Name });
    }

    SkillUpdateModel GetSkillCreateModelFromForm(string code)
    {
        return new()
        {
            Name = Input.Name,
            Code = code,
            Description = Input.Description,
            UsageText = Input.Usage,
            SourcePackageVersionId = SourcePackageVersionId,
            Restricted = false
        };
    }

    async Task<bool> InitializePage(string? language, int? fromPackageId)
    {
        if (fromPackageId is not null)
        {
            var sourcePackage = await _packageRepository.GetAsync(fromPackageId.Value);
            if (sourcePackage is null)
            {
                return false;
            }

            var botName = HttpContext.GetCurrentOrganization().GetBotName();

            PackageBeingInstalled = new PackageDetailsViewModel(sourcePackage, botName);

            Code = PackageBeingInstalled.Code;
            Language = PackageBeingInstalled.Language;
            return true;
        }
        Language = GetLanguage(language);
        return true;
    }

    public class InputModel
    {
        [StringLength(38, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 38 characters in length.")]
        [RegularExpression(Skill.ValidNamePattern, ErrorMessage = Skill.NameErrorMessage)]
        [Remote(action: "Validate", controller: "SkillValidation", areaName: "InternalApi", AdditionalFields = "Type")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }
        [Display(Description = "Provide examples of usage. Simple markdown formatting allowed such as using backticks ` to surround the usage example and _ to surround the description of what the usage example does.")]
        public string? Usage { get; set; }
        public string Type => nameof(Skill);
    }

    static CodeLanguage GetLanguage(string? language)
    {
        return language is not null && Enum.TryParse<CodeLanguage>(language, ignoreCase: true, out var result)
            ? result
            : CodeLanguage.CSharp;
    }
}
