using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.AspNetCore;

namespace Serious.Abbot.Pages.Settings.Account;

public class ApiKeysPage : UserPage
{
    public static readonly DomId ApiKeysContainerId = new("api-keys");
    readonly IUserRepository _userRepository;

    public ApiKeysPage(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public bool Regenerated { get; set; }

    public int CreatedOrRegeneratedApiKeyId { get; set; }


    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<ApiKeyViewModel> ApiKeys { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        await InitializePageAsync(null);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var (member, _) = await InitializePageAsync(null);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var apiKey = await _userRepository.CreateApiKeyAsync(Input.Name, Input.ExpiresIn, member);

        if (Request.IsTurboRequest())
        {
            return TurboAppend(ApiKeysContainerId, Partial("_ApiKeyRow", new ApiKeyViewModel(apiKey, true)));
        }

        CreatedOrRegeneratedApiKeyId = apiKey.Id;

        // Reload the api keys
        await InitializePageAsync(null);
        return Page();
    }

    public async Task<IActionResult> OnPostRegenerateAsync(int id)
    {
        var (_, apiKey) = await InitializePageAsync(id);
        if (apiKey is null)
        {
            return NotFound();
        }

        await _userRepository.RegenerateApiKeyAsync(apiKey);

        if (Request.IsTurboRequest())
        {
            return TurboReplace(apiKey.GetDomId(), Partial("_ApiKeyRow", new ApiKeyViewModel(apiKey, true)));
        }

        CreatedOrRegeneratedApiKeyId = apiKey.Id;
        Regenerated = true;

        // Reload the api keys
        await InitializePageAsync(null);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var (member, apiKey) = await InitializePageAsync(id);
        if (apiKey is null)
        {
            return NotFound();
        }

        await _userRepository.DeleteApiKeyAsync(apiKey, member);
        if (Request.IsTurboRequest())
        {
            return TurboRemove(apiKey.GetDomId());
        }

        return RedirectWithStatusMessage("API key deleted");
    }

    async Task<(Member, ApiKey?)> InitializePageAsync(int? id)
    {
        var apiKeys = await _userRepository.GetApiKeysAsync(Viewer);
        ApiKeys = apiKeys
            .OrderBy(k => k.Name)
            .Select(key => new ApiKeyViewModel(key, key.Id == CreatedOrRegeneratedApiKeyId))
            .ToList();

        var apiKey = id is null
            ? null
            : apiKeys.SingleOrDefault(key => key.Id == id);

        return (Viewer, apiKey);
    }

    public class InputModel
    {
        /// <summary>
        /// The Id of the key, if existing.
        /// </summary>
        public int? Id { get; set; }

        /// <summary>
        /// The name of the key
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// The number of days to expire
        /// </summary>
        [Display(Name = "Expires In")]
        public int ExpiresIn { get; set; } = 365;
    }
}
