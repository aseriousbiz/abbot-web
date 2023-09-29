using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Configuration;

/// <summary>
/// The endpoints for the Abbot Skill Runners. This looks in the database first, then in App Settings if
/// it's not found there.
/// </summary>
public interface IRunnerEndpointManager
{
    /// <summary>
    /// Gets the skill runner endpoint for the given <see cref="Organization"/> and <see cref="CodeLanguage"/>,
    /// taking in to account organization-specific and global settings.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to get endpoints for.</param>
    /// <param name="language">The <see cref="CodeLanguage"/> of the runner to get the endpoint information for.</param>
    /// <returns></returns>
    ValueTask<SkillRunnerEndpoint> GetEndpointAsync(Organization organization, CodeLanguage language);

    /// <summary>
    /// Sets the global override for the given <see cref="CodeLanguage"/>.
    /// This will override the endpoint specified in app config, but NOT any organization-specific endpoint.
    /// </summary>
    /// <param name="language">The <see cref="CodeLanguage"/> of the runner to set the endpoint information for.</param>
    /// <param name="endpoint">The <see cref="SkillRunnerEndpoint"/> to set as the override.</param>
    /// <param name="actor">The <see cref="Member"/> who is taking this action.</param>
    Task SetGlobalOverrideAsync(CodeLanguage language, SkillRunnerEndpoint endpoint, Member actor);

    /// <summary>
    /// Gets all the endpoints specified in app config.
    /// </summary>
    Task<IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint>> GetAppConfigEndpointsAsync();

    /// <summary>
    /// Gets all the endpoints overridden in global settings.
    /// If an endpoint is not overridden, it will not be present in this dictionary.
    /// </summary>
    Task<IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint>> GetGlobalOverridesAsync();

    /// <summary>
    /// Clears the global override for the given <see cref="CodeLanguage"/>.
    /// </summary>
    /// <param name="language">The <see cref="CodeLanguage"/> of the runner to clear the endpoint information for.</param>
    /// <param name="actor">The <see cref="Member"/> who is taking this action.</param>
    Task ClearGlobalOverrideAsync(CodeLanguage language, Member actor);
}

/// <summary>
/// Describes configuration for a custom skill endpoint.
/// </summary>
/// <param name="Url">The URL of the endpoint.</param>
/// <param name="ApiToken">The API token used to authenticate with the endpoint.</param>
/// <param name="IsHosted">Indicates if the runner is considered a "hosted" runner, owned and operated by A Serious Business staff.</param>
// I don't really want IsHosted stored in the database, but I don't want to have to write a wrapper to communicate that a runner is "Hosted" vs "Custom" to the skill runner client -anurse
public record SkillRunnerEndpoint(Uri Url, string? ApiToken, bool IsHosted = false);

public class RunnerEndpointManager : IRunnerEndpointManager
{
    const string RunnerEndpointsSettingName = "RunnerEndpoints";

    readonly ISettingsManager _settingsManager;
    readonly IOptionsSnapshot<SkillOptions> _options;

    public RunnerEndpointManager(ISettingsManager settingsManager, IOptionsSnapshot<SkillOptions> options)
    {
        _settingsManager = settingsManager;
        _options = options;
    }

    public async ValueTask<SkillRunnerEndpoint> GetEndpointAsync(Organization organization, CodeLanguage language)
    {
        // Check if there is an organization-specific endpoint for this language.
        if (organization.Settings.SkillEndpoints.TryGetValue(language, out var orgEndpoints))
        {
            return orgEndpoints;
        }

        // Check the global setting
        if (await GetOverrideEndpointAsync(language) is { } overrideEndpoint)
        {
            return overrideEndpoint;
        }

        // Use the app config endpoint.
        return GetAppConfigEndpoint(language);
    }

    public async Task SetGlobalOverrideAsync(CodeLanguage language, SkillRunnerEndpoint endpoint, Member actor)
    {
        var overrides = await GetOverrideSettingValue();
        overrides[language] = endpoint;
        await _settingsManager.SetWithAuditingAsync(
            SettingsScope.Global,
            RunnerEndpointsSettingName,
            JsonConvert.SerializeObject(overrides),
            actor.User,
            actor.Organization);
    }

    public async Task<IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint>> GetAppConfigEndpointsAsync()
    {
        // We can cache this if necessary, but I doubt constructing this dictionary is a bottleneck.
        return new Dictionary<CodeLanguage, SkillRunnerEndpoint>()
        {
            [CodeLanguage.CSharp] = GetAppConfigEndpoint(CodeLanguage.CSharp),
            [CodeLanguage.JavaScript] = GetAppConfigEndpoint(CodeLanguage.JavaScript),
            [CodeLanguage.Python] = GetAppConfigEndpoint(CodeLanguage.Python),
            [CodeLanguage.Ink] = GetAppConfigEndpoint(CodeLanguage.Ink),
        };
    }

    public async Task<IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint>> GetGlobalOverridesAsync()
    {
        // We can cache this if necessary, but I doubt constructing this dictionary is a bottleneck.
        return await GetOverrideSettingValue();
    }

    public async Task ClearGlobalOverrideAsync(CodeLanguage language, Member actor)
    {
        var overrides = await GetOverrideSettingValue();
        overrides.Remove(language);
        await _settingsManager.SetWithAuditingAsync(
            SettingsScope.Global,
            RunnerEndpointsSettingName,
            JsonConvert.SerializeObject(overrides),
            actor.User,
            actor.Organization);
    }

    SkillRunnerEndpoint GetAppConfigEndpoint(CodeLanguage language)
    {
        switch (language)
        {
            case CodeLanguage.CSharp:
                return new(
                    new(_options.Value.DotNetEndpoint.Require("Required setting 'Skill:DotNetEndpoint' is missing")),
                    _options.Value.DotNetEndpointCode,
                    IsHosted: true);
            case CodeLanguage.JavaScript:
                return new(
                    new(_options.Value.JavaScriptEndpoint.Require("Required setting 'Skill:JavaScriptEndpoint' is missing")),
                    _options.Value.JavaScriptEndpointCode,
                    IsHosted: true);
            case CodeLanguage.Python:
                return new(
                    new(_options.Value.PythonEndpoint.Require("Required setting 'Skill:PythonEndpoint' is missing")),
                    _options.Value.PythonEndpointCode,
                    IsHosted: true);
            case CodeLanguage.Ink:
                return new(
                    new(_options.Value.InkEndpoint.Require("Required setting 'Skill:InkEndpoint' is missing")),
                    _options.Value.InkEndpointCode,
                    IsHosted: true);
            default:
                throw new ArgumentOutOfRangeException(nameof(language));
        }
    }

    async Task<SkillRunnerEndpoint?> GetOverrideEndpointAsync(CodeLanguage language)
    {
        var overrideSetting = await GetOverrideSettingValue();
        if (overrideSetting.TryGetValue(language, out var endpoint))
        {
            // Don't rely on the IsHosted flag in the database.
            return endpoint with { IsHosted = true };
        }
        return null;
    }

    async Task<Dictionary<CodeLanguage, SkillRunnerEndpoint>> GetOverrideSettingValue()
    {
        var setting = await _settingsManager.GetAsync(SettingsScope.Global, RunnerEndpointsSettingName);
        if (setting is null)
        {
            return new();
        }
        return JsonConvert.DeserializeObject<Dictionary<CodeLanguage, SkillRunnerEndpoint>>(setting.Value) ?? new();
    }
}
