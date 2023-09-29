using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;

namespace Serious.Abbot.Pages.Skills.Patterns;

public class AllPageModel : CustomSkillPageModel
{
    public static readonly DomId MatchingPatternsId = new("matching-patterns");

    readonly IPatternRepository _repository;
    readonly ISkillPatternMatcher _patternMatcher;

    public IReadOnlyList<SkillPattern> Patterns { get; private set; } = Array.Empty<SkillPattern>();

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool FilterApplied => Input.Text is { Length: > 0 };

    public AllPageModel(
        IPatternRepository repository,
        IUserRepository userRepository,
        ISkillPatternMatcher patternMatcher)
    {
        _repository = repository;
        _patternMatcher = patternMatcher;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        Patterns = await _repository.GetAllAsync(Organization, enabledPatternsOnly: true);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.Text is not { Length: > 0 })
        {
            return RedirectToPage();
        }

        var patterns = await _patternMatcher.GetMatchingPatternsAsync(
            Input,
            Viewer,
            Organization);

        Patterns = patterns;
        return TurboUpdate(MatchingPatternsId, "_MatchingPatterns", this);
    }

    public class InputModel : IPatternMatchableMessage
    {
        [Required]
        public string Text { get; set; } = null!;
    }
}
