using System;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Integrations.HubSpot;

/// <summary>
/// A simple wrapper class we can "new" up to wrap <see cref="ISettingsManager"/> to manage the HubSpot Form
/// settings.
/// </summary>
public static class FormSettingsExtensions
{
    public const string HubSpotFormKey = "system.create_hubspot_ticket";

    const string HubSpotFormSettingsKey = "hubspot-form-settings";

    public static async Task<FormSettings?> GetHubSpotFormSettingsAsync(
        this ISettingsManager settingsManager,
        Organization organization)
    {
        var setting = await settingsManager.GetAsync(
            SettingsScope.Form(organization, HubSpotFormKey),
            name: HubSpotFormSettingsKey);

        return setting?.Value is { Length: > 0 } settingValue
            ? FormSettings.Parse(settingValue)
            : null;
    }

    public static async Task SetHubSpotFormSettingsAsync(
        this ISettingsManager settingsManager,
        FormSettings formSettings,
        Organization organization,
        User actor)
    {
        await settingsManager.SetAsync(
            SettingsScope.Form(organization, HubSpotFormKey),
            name: HubSpotFormSettingsKey,
            formSettings.ToString(),
            actor);
    }

    public static async Task RemoveHubSpotFormSettingsAsync(
        this ISettingsManager settingsManager,
        Organization organization,
        User actor)
    {
        await settingsManager.RemoveAsync(
            SettingsScope.Form(organization, HubSpotFormKey),
            name: HubSpotFormSettingsKey,
            actor);
    }
}

/// <summary>
/// Settings used when creating a HubSpot ticket via the
/// <see href="https://legacydocs.hubspot.com/docs/methods/forms/submit_form_v3_authentication">Form Submission API</see>.
/// </summary>
/// <param name="HubSpotFormGuid">The HubSpot Form identifier.</param>
/// <param name="TokenFormField">
/// The Form field where we place a token that we search for in order to match up the created ticket with the source
/// Conversation.
/// </param>
public record FormSettings(string HubSpotFormGuid, string TokenFormField)
{
    /// <summary>
    /// Parse a setting value into a <see cref="FormSettings" />.
    /// </summary>
    /// <param name="settingValue"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static FormSettings Parse(string settingValue)
    {
        if (settingValue.Split('|', 2) is [var formGuid, var tokenField])
        {
            return new FormSettings(formGuid, tokenField);
        }
        throw new ArgumentException(
            message: $"Setting value {settingValue} is not in the expected format for {nameof(FormSettings)}.",
            nameof(settingValue));
    }

    /// <summary>
    /// Output the <see cref="FormSettings"/> as a string that can be parsed by <see cref="Parse"/>.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"{HubSpotFormGuid}|{TokenFormField}";
}
