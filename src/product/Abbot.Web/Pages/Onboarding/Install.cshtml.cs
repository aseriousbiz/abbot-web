using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using OpenAI_API.Models;
using Serious.Abbot.Entities;
using Serious.Abbot.Onboarding;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

namespace Serious.Abbot.Pages.Onboarding;

public class InstallPageModel : UserPage
{
    readonly IOrganizationRepository _organizationRepository;
    readonly IRoomRepository _roomRepository;
    readonly IConversationRepository _conversationRepository;
    readonly OnboardingService _onboardingService;

    public bool IsBotInstalled { get; set; }
    public bool InvitedToRoom { get; set; }

    public bool HasEnabledTracking { get; set; }

    [Display(Name = "Intended Customer Type")]
    [Required(ErrorMessage = "Choose your primary customer type so we can help configure Abbot for you.")]
    [BindProperty]
    public IntendedCustomerType IntendedCustomerType { get; set; }

    [Display(Name = "Channel name")]
    [Required(ErrorMessage = "Without a channel name, we won't be able to keep you up to date on your customers!")]
    [BindProperty]
    public string DefaultHubRoomName { get; set; } = null!;

    public InstallPageModel(
        IOrganizationRepository organizationRepository,
        IRoomRepository roomRepository,
        IConversationRepository conversationRepository,
        OnboardingService onboardingService)
    {
        _organizationRepository = organizationRepository;
        _roomRepository = roomRepository;
        _conversationRepository = conversationRepository;
        _onboardingService = onboardingService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        IntendedCustomerType = Organization.Settings.IntendedCustomerType;
        DefaultHubRoomName = Organization.Settings.OnboardingDefaultHubRoomName ?? string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Organization.Settings = Organization.Settings with
        {
            IntendedCustomerType = IntendedCustomerType,
            OnboardingDefaultHubRoomName = DefaultHubRoomName,
        };

        await _organizationRepository.SaveChangesAsync();
        return await _onboardingService.RedirectForSlackInstallAsync(Organization, DefaultHubRoomName, Viewer);
    }

    public async Task<IActionResult> OnPostSkipAsync()
    {
        await _onboardingService.SkipOnboardingAsync(Organization, Viewer);
        StatusMessage =
            "Onboarding skipped!";
        return RedirectToPage("/Index");
    }
}
