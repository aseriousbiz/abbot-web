using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using OpenAI_API.Models;
using Serious.Abbot.Events;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.AspNetCore;
using Serious.Logging;

namespace Serious.Abbot.Pages.Staff;

public class ToolsPage : StaffToolsPage
{
    readonly ISensitiveLogDataProtector _dataProtector;
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly IHostEnvironment _hostEnvironment;

    public DomId DecryptResults { get; } = new("decrypt-results");

    public string? Unencrypted { get; private set; }

    [BindProperty]
    public string? Question { get; set; }

    public string? Answer { get; private set; }

    public IReadOnlyList<Model> Models { get; private set; } = Array.Empty<Model>();

    public ToolsPage(
        ISensitiveLogDataProtector dataProtector,
        IUserRepository userRepository,
        IRoleManager roleManager,
        IHostEnvironment hostEnvironment)
    {
        _dataProtector = dataProtector;
        _userRepository = userRepository;
        _roleManager = roleManager;
        _hostEnvironment = hostEnvironment;
    }

    public IActionResult OnPostDecrypt(string ciphertext)
    {
        try
        {
            Unencrypted = _dataProtector.Unprotect(ciphertext);
        }
        catch (CryptographicException)
        {
            Unencrypted = "<< content could not be decrypted >>";
        }

        if (Request.IsAjaxRequest())
        {
            return TurboUpdate(DecryptResults, WebUtility.HtmlEncode(Unencrypted));
        }

        StatusMessage = "Data decrypted successfully.";
        return Page();
    }

    public async Task<IActionResult> OnPostCreateTestUsersAsync()
    {
        if (_hostEnvironment.IsProduction())
        {
            return NotFound();
        }

        var users = new Tuple<string, string>[]
        {
            // THE ORDER IS IMPORTANT.
            new("Bugs Bunny", "https://pbs.twimg.com/profile_images/1564953421488193537/9b7QYNGg_400x400.jpg"),
            new("The Bride", "https://user-images.githubusercontent.com/19977/195901981-34e1189c-91f0-411c-bd19-c81430301df4.png"),
            new("Valkyrie", "https://user-images.githubusercontent.com/19977/195903847-4488acb4-c86e-465e-b5b3-917598dcb359.png"),
            new("Dora", "https://user-images.githubusercontent.com/19977/195903946-23a94f5a-925d-44f8-a046-73cf66c5ff1d.png"),
            new("Miss Piggy", "https://pbs.twimg.com/profile_images/1455168841416417283/zn-Qew_4_400x400.jpg"),
            new("Gandalf", "https://pbs.twimg.com/profile_images/461235088597196800/xwJjsihE_400x400.jpeg"),
            new("Jerry", "https://pbs.twimg.com/profile_images/2161728067/image_400x400.jpg"),
            new("Kermit", "https://pbs.twimg.com/profile_images/1455169155733377027/Eczv5-Jb_400x400.jpg"),
            new("Neo", "https://user-images.githubusercontent.com/19977/195899593-002c4ff4-d6b1-4b1e-b0a1-d961fef3c0f2.jpeg"),
            new("Mr. T", "https://pbs.twimg.com/profile_images/3160816489/a0bbfcd09d9b7dbc56b76a07130972cf_400x400.png"),
            new("Tyrion", "https://pbs.twimg.com/profile_images/668279339838935040/8sUE9d4C_400x400.jpg"),
            new("Danearys", "https://user-images.githubusercontent.com/19977/196767292-59e3223b-8e7a-41ed-af6f-49135e7d03ff.png"),
            new("Jon Snow", "https://user-images.githubusercontent.com/19977/196767404-80366d25-7c8f-49c4-8764-45e7d3c851da.png"),
            new("Peter Griffin", "https://pbs.twimg.com/profile_images/2959799296/8adc8e7914393f0716a18e133e217dd9_400x400.jpeg"),
            new("Lois", "https://user-images.githubusercontent.com/19977/196766615-0ec65612-3747-45b0-aeab-9849443d7f15.png"),
            new("Brian", "https://user-images.githubusercontent.com/19977/196766821-1deee15b-c179-4e7b-ac41-a61a7d37e8e7.png"),
            new("Stewie", "https://user-images.githubusercontent.com/19977/196766923-b2e1319b-324c-4e5f-9ea1-633491054cb5.png"),
            new("Meg", "https://user-images.githubusercontent.com/19977/196767094-5d4d3583-bc54-4ac1-90a7-0e396e5119a5.png"),
            new("Chris", "https://user-images.githubusercontent.com/19977/196767144-ccd5899a-2450-4573-8fa1-f5c11f6e0034.png"),
        };

        string? errors = null;
        int index = 0;
        foreach (var (username, avatar) in users)
        {
            var userId = $"UTEST00000{index++}";
            var userEventPayload = new UserEventPayload(
                userId,
                Viewer.Organization.PlatformId,
                username,
                username,
                Avatar: avatar);

            try
            {
                var member = await _userRepository.EnsureAndUpdateMemberAsync(userEventPayload, Viewer.Organization);
                member.User.NameIdentifier = $"slack|oauth|{userId}-{Viewer.Organization.PlatformId}";
                await _userRepository.UpdateUserAsync();
                await _roleManager.AddUserToRoleAsync(member, Roles.Agent, Viewer);
            }
            catch (Exception ex)
            {
                errors += ex.ToString();
                Debug.WriteLine(ex);
            }
        }

        StatusMessage = $"Done! Errors: {errors ?? "None"}";
        return RedirectToPage();
    }
}
