using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing.Messages;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Onboarding;

public class OnboardingService
{
    readonly IRoomRepository _roomRepository;
    readonly IOrganizationRepository _organizationRepository;
    readonly ISettingsManager _settingsManager;
    readonly IPublishEndpoint _publishEndpoint;

    public OnboardingService(IRoomRepository roomRepository, IOrganizationRepository organizationRepository, ISettingsManager settingsManager, IPublishEndpoint publishEndpoint)
    {
        _roomRepository = roomRepository;
        _organizationRepository = organizationRepository;
        _settingsManager = settingsManager;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<IActionResult> RedirectForSlackInstallAsync(Organization organization, string defaultHubRoomName, Member actor)
    {
        // Save a setting to trigger the default hub to be created
        await _settingsManager.SetAsync(
            SettingsScope.Organization(organization),
            "CreateDefaultHub",
            defaultHubRoomName,
            actor.User,
            TimeSpan.FromMinutes(15));

        return new RedirectToActionResult("Install", "Slack", null);
    }

    public async Task SkipOnboardingAsync(Organization organization, Member actor)
    {
        await _organizationRepository.SetOnboardingStateAsync(organization, OnboardingState.Skipped, actor);
    }

    /// <summary>
    /// Updates onboarding state for the organization and returns an <see cref="IActionResult"/>
    /// describing the next action for the user to take to complete onboarding.
    /// If the organization is now fully onboarded, returns null.
    /// </summary>
    public async Task<IActionResult?> UpdateOnboardingStateAsync(Organization organization, Member actor)
    {
        if (!organization.IsOnboarding())
        {
            return null;
        }

        if (!organization.IsBotInstalled())
        {
            // We need to install the bot
            return new RedirectToPageResult("/Onboarding/Install");
        }

        // Check if we need to create a default hub
        var createHubSetting = await _settingsManager.GetAsync(
            SettingsScope.Organization(organization),
            "CreateDefaultHub");
        if (createHubSetting is not null)
        {
            await _publishEndpoint.Publish(new CreateDefaultHub()
            {
                OrganizationId = organization,
                ActorId = actor,
                Name = createHubSetting.Value,
            });

            await _settingsManager.RemoveAsync(SettingsScope.Organization(organization), "CreateDefaultHub", actor.User);
        }

        // TODO: We can add additional onboarding steps here.
        // Onboarding is complete
        await _organizationRepository.SetOnboardingStateAsync(organization, OnboardingState.Completed, actor);
        return null;
    }
}
