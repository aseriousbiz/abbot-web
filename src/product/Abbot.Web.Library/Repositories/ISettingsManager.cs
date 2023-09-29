using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Threading;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Repositories;

public struct SettingsScope : IEquatable<SettingsScope>, IEquatable<string>
{
    public static readonly SettingsScope Global;

    public static SettingsScope Organization(Id<Organization> organizationId) =>
        Global.Child("Organization", organizationId);

    public static SettingsScope HubSpotPortal(long portalId) =>
        Global.Child("HubSpotPortal", portalId);

    public static SettingsScope Room(Room room) => Organization(room.Organization).Child("Room", room.Id);

    public static SettingsScope Member(Member member) => Organization(member.Organization).Child("Member", member.Id);

    public static SettingsScope Form(Organization organization, string formId) => Organization(organization).Child("Form", formId);

    public static SettingsScope Conversation(Conversation conversation) =>
        Conversation(new(conversation.OrganizationId), conversation);

    public static SettingsScope Conversation(Id<Organization> organizationId, Id<Conversation> conversationId) =>
        Organization(organizationId).Child("Conversation", conversationId);

    string? _name;

    // We use the default value to represent global
    // However, the default value will have a null name.
    // In Postgres, 'null != null' and we have a unique index on (Scope,Name)
    // So we can't have either value be null, and we must enforce that Scope is _always_ a non-null string.
    public string Name => _name ?? string.Empty;

    SettingsScope(string name) => _name = name;

    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is SettingsScope other && Equals(other);
    public bool Equals(SettingsScope other) => Name == other.Name;
    public bool Equals(string? other) => Name == other;

    public static bool operator ==(SettingsScope left, SettingsScope right) => left.Equals(right);
    public static bool operator !=(SettingsScope left, SettingsScope right) => !(left == right);
    public static bool operator ==(string left, SettingsScope right) => right.Equals(left);
    public static bool operator !=(string left, SettingsScope right) => !(left == right);
    public static bool operator ==(SettingsScope left, string right) => left.Equals(right);
    public static bool operator !=(SettingsScope left, string right) => !(left == right);

    // Marking these as Pure because they are not mutating any state,
    // and it helps keep Resharper happy that we're calling them on a readonly variable above
    [Pure]
    public SettingsScope Child(string type, long id) => Child(type, id.ToString(CultureInfo.InvariantCulture));

    [Pure]
    public SettingsScope Child(string type, int id) => Child(type, id.ToString(CultureInfo.InvariantCulture));

    [Pure]
    public SettingsScope Child(string type) => Child(type, "");

    [Pure]
    public SettingsScope Child(string type, string name) =>
        _name is not { Length: > 0 }
            ? name is { Length: > 0 } ? new($"{type}:{name}") : new(type)
            : new($"{_name}/{type}:{name}");
}

/// <summary>
/// <para>
/// Manages settings. Some settings are App Settings that should only be used by Staff users.
/// Use <see cref="SetWithAuditingAsync"/> and <see cref="RemoveWithAuditingAsync"/> for those settings to ensure
/// changes are recorded in the Activity Log.
/// </para>
/// <para>
/// Other settings are internal system settings that don't need to be logged to the Activity Log.
/// </para>
/// </summary>
public interface ISettingsManager
{
    /// <summary>
    /// Creates or updates an organization setting by name. Use this for settings that do not require auditing.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The user setting the setting.</param>
    /// <returns>The <see cref="Setting" /> associated with the name.</returns>
    Task<Setting> SetAsync(SettingsScope scope, string name, string value, User actor);

    /// <summary>
    /// Creates or updates an organization setting by name. Use this for settings that do not require auditing.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The user setting the setting.</param>
    /// <param name="ttl">
    /// The length of time the setting is valid for.
    /// After this time, it will not be returned by GetAsync and may be purged by a background job.
    /// </param>
    /// <returns>The <see cref="Setting" /> associated with the name.</returns>
    Task<Setting> SetAsync(SettingsScope scope, string name, string value, User actor, TimeSpan ttl);

    /// <summary>
    /// Removes the setting.  Use this for settings that require do not require auditing.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="actor">The user removing the setting.</param>
    /// <returns>The <see cref="Setting" /> associated with the name.</returns>
    Task RemoveAsync(SettingsScope scope, string name, User actor);

    /// <summary>
    /// Creates or updates setting by name. Use this for settings that require auditing.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="actor">The user setting the setting.</param>
    /// <param name="organization">The staff user organization. We need this to be able to audit log these actions.</param>
    /// <returns>The <see cref="Setting" /> associated with the name.</returns>
    Task<Setting> SetWithAuditingAsync(SettingsScope scope, string name, string value, User actor, Organization organization);

    /// <summary>
    /// Removes the setting.  Use this for settings that require auditing.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    /// <param name="name">The name of the setting.</param>
    /// <param name="actor">The user removing the setting.</param>
    /// <param name="organization">The staff user organization. We need this to be able to audit log these actions.</param>
    /// <returns>The <see cref="Setting" /> associated with the name.</returns>
    Task RemoveWithAuditingAsync(SettingsScope scope, string name, User actor, Organization organization);

    /// <summary>
    /// Retrieves the setting.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    /// <param name="name">The name of the setting.</param>
    /// <returns>The <see cref="Setting" /> associated with the name.</returns>
    Task<Setting?> GetAsync(SettingsScope scope, string name);

    /// <summary>
    /// Retrieves the setting matching the provided name in the first matching scope in <paramref name="scopes" />.
    /// </summary>
    /// <param name="name">The name of the setting.</param>
    /// <param name="scopes">
    /// A list of <see cref="SettingsScope"/>s to consider.
    /// The first one of these that has a setting with the provided name will be used.
    /// </param>
    /// <returns>The <see cref="Setting"/> associated with the name.</returns>
    Task<Setting?> GetCascadingAsync(string name, params SettingsScope[] scopes);

    /// <summary>
    /// Retrieves all settings in the specified scope.
    /// Includes expired settings.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    Task<IReadOnlyList<Setting>> GetAllAsync(SettingsScope scope);

    /// <summary>
    /// Retrieves all settings in the specified scope that match the provided prefix.
    /// </summary>
    /// <param name="scope">The <see cref="SettingsScope"/> for which the setting applies.</param>
    /// <param name="prefix">The prefix to match.</param>
    Task<IReadOnlyList<Setting>> GetAllAsync(SettingsScope scope, string prefix);

    /// <summary>
    /// Removes all expired settings, as of the provided time.
    /// </summary>
    /// <param name="asOfUtc">The UTC time to use as "now" when evaluating setting expiry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of settings deleted</returns>
    Task<int> RemoveExpiredSettingsAsync(DateTime asOfUtc, CancellationToken cancellationToken);
}
