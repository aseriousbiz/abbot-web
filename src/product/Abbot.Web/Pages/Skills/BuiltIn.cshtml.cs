using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Metadata;
using Serious.Abbot.Services;

namespace Serious.Abbot.Pages.Skills;

public class BuiltInPage : PageModel
{
    readonly IBuiltinSkillRegistry _builtinSkillRegistry;

    public BuiltInPage(IBuiltinSkillRegistry builtinSkillRegistry)
    {
        _builtinSkillRegistry = builtinSkillRegistry;
    }

    public IReadOnlyList<ISkillDescriptor> BuiltInSkills { get; private set; } = null!;

    public void OnGetAsync()
    {
        BuiltInSkills = _builtinSkillRegistry.SkillDescriptors.Where(d => !d.Hidden).ToReadOnlyList();
    }
}
