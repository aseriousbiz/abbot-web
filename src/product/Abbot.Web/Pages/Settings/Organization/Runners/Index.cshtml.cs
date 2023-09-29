using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Settings.Organization.Runners;

public class IndexPage : UserPage
{
    readonly IOrganizationRepository _organizationRepository;
    internal static readonly
        IReadOnlyDictionary<CodeLanguage, string>
        ConfigurableLanguages = new Dictionary<CodeLanguage, string>()
        {
            [CodeLanguage.CSharp] = ".NET",
            [CodeLanguage.Python] = "Python",
            [CodeLanguage.JavaScript] = "JavaScript",
            // Ignoring Ink for now. Nobody is using it at the moment.
        };

    public IReadOnlyList<(CodeLanguage Language, string DisplayName, Uri? CurrentUrl)> CurrentConfig
    {
        get;
        private set;
    } = null!;

    public IndexPage(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task OnGetAsync(string? endpoint)
    {
        var currentConfig = new List<(CodeLanguage Language, string DisplayName, Uri? CurrentUrl)>();
        foreach (var (lang, name) in ConfigurableLanguages.OrderBy(p => p.Key))
        {
            currentConfig.Add(Organization.Settings.SkillEndpoints.TryGetValue(lang, out var settings)
                ? (lang, name, settings.Url)
                // The legacy value includes the '?code=' parameter, but we're removing it, so I'm not worried about hiding it.
                : (lang, name, null));
        }

        CurrentConfig = currentConfig;
    }
}
