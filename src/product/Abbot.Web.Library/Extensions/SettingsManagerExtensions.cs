using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Extensions;

/// <summary>
/// Extensions to the <see cref="ISettingsManager" />. Mainly used to retrieve specific settings.
/// </summary>
public static class SettingsManagerExtensions
{
    const string LastVerifiedMessageIdKey = "LastVerifiedMessageId";
    /// <summary>
    /// Retrieves the last message Id for the room that the job to report untracked conversations has examined.
    /// </summary>
    /// <param name="settingsManager">The <see cref="ISettingsManager"/>.</param>
    /// <param name="room">The room for which we examined.</param>
    public static async Task<string?> GetLastVerifiedMessageIdAsync(
        this ISettingsManager settingsManager,
        Room room)
    {
        var setting = await settingsManager.GetAsync(SettingsScope.Room(room), LastVerifiedMessageIdKey);
        if (setting is null)
        {
            // Try the old key, if it matches, we'll be writing the new one when we're done.
            // TODO: Can remove this code once we converge on the new key.
            var oldKey = $"LastVerifiedMessageId:{room.Organization.Id}:{room.Id}";
            setting = await settingsManager.GetAsync(SettingsScope.Global, oldKey);
        }

        return setting?.Value;
    }

    /// <summary>
    /// Sets the last message Id for the room that the job to report untracked conversations has examined.
    /// </summary>
    /// <param name="settingsManager">The <see cref="ISettingsManager"/>.</param>
    /// <param name="messageId">The last verified messageId.</param>
    /// <param name="actor">The user setting the endpoint.</param>
    /// <param name="room">The room for which we examined.</param>
    public static async Task SetLastVerifiedMessageIdAsync(
        this ISettingsManager settingsManager,
        Room room,
        string? messageId,
        User actor)
    {
        var scope = SettingsScope.Room(room);
        if (messageId is not null)
        {
            await settingsManager.SetAsync(
                scope,
                name: LastVerifiedMessageIdKey,
                value: messageId,
                actor);
        }
        else
        {
            await settingsManager.RemoveAsync(scope, name: LastVerifiedMessageIdKey, actor);
        }
    }

    /// <summary>
    /// Retrieves a boolean setting. If the value is missing or not a boolean, returns <c>false</c>.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="scope">The scope of the setting.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="defaultIfNull">The value to return if the setting is null.</param>
    /// <returns><c>true</c> if the setting value is present and "True", otherwise <c>false</c>.</returns>
    public static async Task<bool> GetBooleanValueAsync(
        this ISettingsManager settingsManager,
        SettingsScope scope,
        string name,
        bool defaultIfNull = false)
    {
        var setting = await settingsManager.GetAsync(scope, name);
        return setting is null
            ? defaultIfNull
            : bool.Parse(setting.Value);
    }

    /// <summary>
    /// Retrieves a boolean setting. If the value is missing or not a boolean, returns <c>false</c>.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="scope">The scope of the setting.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The actor setting the value.</param>
    /// <returns><c>true</c> if the setting value is present and "True", otherwise <c>false</c>.</returns>
    public static async Task<Setting> SetBooleanValueAsync(this ISettingsManager settingsManager,
        SettingsScope scope,
        string name,
        bool value,
        User actor)
    {
        return await settingsManager.SetAsync(scope, name, $"{value}", actor);
    }

    /// <summary>
    /// Sets a boolean valued setting.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="scope">The scope of the setting.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The actor setting the value.</param>
    /// <param name="organization">The user organization. We need this to be able to audit log these actions.</param>
    /// <returns><c>true</c> if the setting value is present and "True", otherwise <c>false</c>.</returns>
    public static async Task<Setting> SetBooleanValueWithAuditing(
        this ISettingsManager settingsManager,
        SettingsScope scope,
        string name,
        bool value,
        User actor,
        Organization organization)
    {
        return await settingsManager.SetWithAuditingAsync(scope, name, $"{value}", actor, organization);
    }

    /// <summary>
    /// Retrieves an integer setting. If the value is missing or not an integer, returns the default value.
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="scope">The scope of the setting.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="defaultIfNull">The value to return if the setting is null.</param>
    /// <returns>The setting value as an integer if the setting value is present, otherwise the <paramref name="defaultIfNull"/>.</returns>
    public static async Task<int> GetIntegerValueAsync(
        this ISettingsManager settingsManager,
        SettingsScope scope,
        string name,
        int defaultIfNull = 0)
    {
        var setting = await settingsManager.GetAsync(scope, name);
        return setting is null
            ? defaultIfNull
            : int.TryParse(setting.Value, out var intValue)
                ? intValue
                : defaultIfNull;
    }

    /// <summary>
    /// Sets an integer value as the setting value..
    /// </summary>
    /// <param name="settingsManager">The settings manager.</param>
    /// <param name="scope">The scope of the setting.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The actor setting the value.</param>
    /// <returns><c>true</c> if the setting value is present and "True", otherwise <c>false</c>.</returns>
    public static async Task<Setting> SetIntegerValueAsync(
        this ISettingsManager settingsManager,
        SettingsScope scope,
        string name,
        int value,
        User actor)
    {
        return await settingsManager.SetAsync(scope, name, $"{value}", actor);
    }
}
