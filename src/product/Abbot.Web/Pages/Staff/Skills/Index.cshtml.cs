using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Staff.Skills;

public class IndexPage : StaffToolsPage
{
    readonly IRunnerEndpointManager _endpoints;

    public IndexPage(IRunnerEndpointManager endpoints)
    {
        _endpoints = endpoints;
    }

    /// <summary>
    /// The Skill Runner Endpoints configured in AppSettings.
    /// </summary>
    public IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint> AppConfigEndpoints { get; set; } = null!;

    public IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint> OverrideEndpoints { get; set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        AppConfigEndpoints = await _endpoints.GetAppConfigEndpointsAsync();
        OverrideEndpoints = await _endpoints.GetGlobalOverridesAsync();
    }

    public async Task<IActionResult> OnPostAsync(string? language)
    {
        if (Input.Endpoint is null)
        {
            StatusMessage = "Endpoint is required. Press 'Reset' to clear the override.";
            return RedirectToPage();
        };

        if (language is null || !Enum.TryParse<CodeLanguage>(language, ignoreCase: true, out var codeLanguage))
        {
            return BadRequest();
        }

        await _endpoints.SetGlobalOverrideAsync(
            codeLanguage,
            new(Input.Endpoint, Input.Code, true),
            Viewer);
        StatusMessage = $"Global override for {codeLanguage} runner set.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetAsync(string? language)
    {
        if (language is null || !Enum.TryParse<CodeLanguage>(language, ignoreCase: true, out var codeLanguage))
        {
            return BadRequest();
        }

        await _endpoints.ClearGlobalOverrideAsync(codeLanguage, Viewer);
        StatusMessage = $"Global override for {codeLanguage} runner cleared.";
        return RedirectToPage(null, new { language = (string?)null });
    }

    public class InputModel
    {
        public Uri? Endpoint { get; set; }
        public string? Code { get; set; }
    }
}
