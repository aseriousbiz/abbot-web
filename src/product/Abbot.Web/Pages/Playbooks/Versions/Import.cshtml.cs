using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Playbooks.Versions;

public class ImportModel : StaffViewablePage
{
    const int MaximumAllowedPlaybookSize = 1024 * 1024; // 1 MiB
    readonly PlaybookRepository _playbookRepository;
    readonly IHttpClientFactory _clientFactory;
    readonly ILogger<ImportModel> _logger;

    public ImportModel(PlaybookRepository playbookRepository, IHttpClientFactory clientFactory, ILogger<ImportModel> logger)
    {
        _playbookRepository = playbookRepository;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public Playbook? Playbook { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = null!;

    public async Task OnGetAsync(string? slug)
    {
        Playbook = slug is not null
            ? await _playbookRepository.GetBySlugAsync(slug, Organization)
            : null;
    }

    [AllowStaff]
    public async Task<IActionResult> OnPostAsync(string? slug)
    {
        Playbook = slug is not null
            ? await _playbookRepository.GetBySlugAsync(slug, Organization)
            : null;

        ValidatePost();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        string definition;
        if (Input.DefinitionUrl is { Length: > 0 })
        {
            using var client = _clientFactory.CreateClient();
            var response = await client.GetAsync(Input.DefinitionUrl);
            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError("Input.DefinitionUrl", "The URL could not be downloaded.");
                return Page();
            }

            if (response.Content.Headers.ContentLength > MaximumAllowedPlaybookSize)
            {
                ModelState.AddModelError("Input.DefinitionUrl", "The playbook is too large.");
                return Page();
            }

            definition = await response.Content.ReadAsStringAsync();
        }
        else
        {
            await using var stream = Input.Content.Require().OpenReadStream();
            if (stream.Length > MaximumAllowedPlaybookSize)
            {
                ModelState.AddModelError("Input.Content", "The playbook is too large.");
                return Page();
            }
            using var reader = new StreamReader(stream);
            definition = await reader.ReadToEndAsync();
        }

        // Validate the playbook
        try
        {
            var playbookDefinition = PlaybookFormat.Deserialize(definition);
            if (PlaybookFormat.Validate(playbookDefinition) is { Count: > 0 } errors)
            {
                _logger.InvalidPlaybook(string.Join("\n", errors.Select(e => e.Message)));
                ModelState.AddModelError("Input.Content", "The playbook is invalid.");
                return Page();
            }
        }
        catch (JsonException jex)
        {
            _logger.InvalidPlaybookJson(jex);
            ModelState.AddModelError("Input.Content", "The playbook is invalid.");
            return Page();
        }

        if (Playbook is null)
        {
            // We need to create a new playbook
            var result = await _playbookRepository.CreateAsync(
                Input.Name.Require(),
                Input.Description.Require(),
                Input.Name.Require().ToSlug(),
                enabled: true,
                Viewer,
                Input.StaffReason);

            if (result.Type == EntityResultType.Conflict)
            {
                ModelState.AddModelError("Input.Name", "A playbook with that name already exists.");
                return Page();
            }

            if (!result.IsSuccess)
            {
                StatusMessage = $"Failed to create playbook: {result.ErrorMessage}.";
                return RedirectToPage();
            }

            Playbook = result.Entity;
        }

        var comment = InStaffTools
            ? "Imported by Abbot Staff"
            : $"Imported by {Viewer.DisplayName}";
        var version = await _playbookRepository.CreateVersionAsync(
            Playbook,
            definition,
            comment,
            Viewer,
            Input.StaffReason);

        // Take the user to the viewer for this playbook
        StatusMessage = "Playbook definition imported";
        return RedirectToPage("/Playbooks/View",
            new {
                slug = Playbook.Slug,
                staffOrganizationId = InStaffTools
                    ? Organization.PlatformId
                    : null,
            });
    }

    void ValidatePost()
    {
        // Validation is a little non-trivial, so just do it manually
        if (Playbook is null)
        {
            if (Input.Name is not { Length: > 0 })
            {
                ModelState.AddModelError("Input.Name", "Name is required");
            }

            if (Input.Description is not { Length: > 0 })
            {
                ModelState.AddModelError("Input.Description", "Description is required");
            }
        }

        if (InStaffTools && Input.StaffReason is not { Length: > 0 })
        {
            ModelState.AddModelError("Input.StaffReason", "A reason is required");
        }

        if (Input.Content is not null && Input.DefinitionUrl is { Length: > 0 })
        {
            ModelState.AddModelError("Input.DefinitionUrl", "You cannot specify both a URL and a file.");
        }

        if (Input.Content is null && Input.DefinitionUrl is not { Length: > 0 })
        {
            ModelState.AddModelError("Input.Content", "A playbook definition is required");
        }
    }

    public record InputModel
    {
        public string? Name { get; init; }

        public string? Description { get; init; }

        [Display(Name = "Reason")]
        public string? StaffReason { get; init; }

        [Display(Name = "Url")]
        public string? DefinitionUrl { get; init; }

        [Display(Name = "Upload")]
        public IFormFile? Content { get; init; }
    }
}

public static partial class ImportModelLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Import failed due to invalid playbook:\n{PlaybookValidationErrors}")]
    public static partial void InvalidPlaybook(this ILogger<ImportModel> logger, string playbookValidationErrors);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Import failed due to invalid playbook")]
    public static partial void InvalidPlaybookJson(this ILogger<ImportModel> logger, Exception ex);
}
