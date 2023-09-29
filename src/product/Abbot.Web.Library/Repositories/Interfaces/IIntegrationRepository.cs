using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The repository for <see cref="Integration"/>s
/// </summary>
public interface IIntegrationRepository
{
    /// <summary>
    /// Creates an integration in the repository and returns it.
    /// Allows creating more than one integration of the given <see cref="IntegrationType"/>.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to create the integration for.</param>
    /// <param name="type">A <see cref="IntegrationType"/> specifying the type of integration to create.</param>
    /// <param name="enabled">Initial value for <see cref="Integration.Enabled"/>.</param>
    Task<Integration> CreateIntegrationAsync(Organization organization, IntegrationType type, bool enabled = false);

    /// <summary>
    /// Ensures the integration exists in the repository, creating it if it does not, and returns it.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to enable the integration for.</param>
    /// <param name="type">A <see cref="IntegrationType"/> specifying the type of integration.</param>
    /// <param name="enabled">Initial value for <see cref="Integration.Enabled"/>.</param>
    Task<Integration> EnsureIntegrationAsync(Organization organization, IntegrationType type, bool enabled = false);

    /// <summary>
    /// Enables an integration for the organization.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to enable the integration for.</param>
    /// <param name="type">A <see cref="IntegrationType"/> specifying the type of integration to enable.</param>
    /// <param name="actor">The person that enabled the integration.</param>
    Task<Integration> EnableAsync(Organization organization, IntegrationType type, Member actor);

    /// <summary>
    /// Disables an integration for the organization.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to disable the integration for.</param>
    /// <param name="type">A <see cref="IntegrationType"/> specifying the type of integration to disable.</param>
    /// <param name="actor">The person that disabled the integration.</param>
    Task DisableAsync(Organization organization, IntegrationType type, Member actor);

    /// <summary>
    /// Gets the integration settings for a specific organization and integration type.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to get the integration for.</param>
    /// <param name="type">A <see cref="IntegrationType"/> specifying the type of integration to retrieve.</param>
    /// <returns>A <see cref="Integration"/> containing integration settings, or <c>null</c> if there is no integration configured.</returns>
    ValueTask<Integration?> GetIntegrationAsync(Organization organization, IntegrationType type);

    /// <summary>
    /// Gets the integration and <typeparamref name="TSettings"/> for a specific organization and integration type.
    /// </summary>
    /// <typeparam name="TSettings">The integration settings type.</typeparam>
    /// <param name="externalId">The externalId to get the integration for.</param>
    /// <returns>A <see cref="Integration"/> and <typeparamref name="TSettings"/> for the organization, or <c>(null, null)</c> if there is no integration configured.</returns>
    ValueTask<(Integration?, TSettings?)> GetIntegrationAsync<TSettings>(string externalId)
        where TSettings : class, IIntegrationSettings;

    /// <summary>
    /// Gets the integration and <typeparamref name="TSettings"/> for a specific organization and integration type.
    /// </summary>
    /// <typeparam name="TSettings">The integration settings type.</typeparam>
    /// <param name="organization">The <see cref="Organization"/> to get the integration for.</param>
    /// <returns>A <see cref="Integration"/> and <typeparamref name="TSettings"/> for the organization, or <c>(null, null)</c> if there is no integration configured.</returns>
    ValueTask<(Integration?, TSettings?)> GetIntegrationAsync<TSettings>(Organization organization)
        where TSettings : class, IIntegrationSettings;

    /// <summary>
    /// Gets the integration settings for a specific ID.
    /// </summary>
    /// <param name="integrationId">The ID of the <see cref="Integration"/> to get.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
    /// <returns>An <see cref="Integration"/> containing integration settings, or <c>null</c>.</returns>
    Task<Integration?> GetIntegrationByIdAsync(int integrationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all integration settings for a specific organization.
    /// After calling this method, the <see cref="Organization.Integrations"/> property on <paramref name="organization"/> is guaranteed to be non-<c>null</c>.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to get integrations for.</param>
    /// <returns>A list of <see cref="Integration"/>s containing integration settings.</returns>
    ValueTask<IReadOnlyList<Integration>> GetIntegrationsAsync(Organization organization);

    /// <summary>
    /// Gets a <see cref="TicketingIntegration"/> for <paramref name="organization"/> by <paramref name="id"/>.
    /// Returns <see langword="null"/> if the <see cref="Integration"/> is for the wrong
    /// <see cref="Organization"/> or it's not a Ticketing integration.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> of the expected <see cref="Integration"/>.</param>
    /// <param name="id">The ID of the <see cref="Integration"/>.</param>
    /// <returns>The matching <see cref="TicketingIntegration"/>, or <see langword="null"/>.</returns>
    Task<TicketingIntegration?> GetTicketingIntegrationByIdAsync(Organization organization, Id<Integration> id);

    /// <summary>
    /// Gets all Ticketing integrations and their settings for a specific organization.
    /// After calling this method, the <see cref="Organization.Integrations"/> property on <paramref name="organization"/> is guaranteed to be non-<c>null</c>.
    /// </summary>
    /// <param name="organization">The <see cref="Organization"/> to get integrations for.</param>
    /// <returns>A list of <see cref="Integration"/>/<see cref="ITicketingSettings"/> pairs.</returns>
    Task<IReadOnlyList<TicketingIntegration>> GetTicketingIntegrationsAsync(Organization organization);

    /// <summary>
    /// Tries to get <see cref="ITicketingSettings"/> for <paramref name="integration"/>.
    /// </summary>
    /// <param name="integration">The <see cref="Integration"/>.</param>
    /// <param name="settings">The <see cref="ITicketingSettings"/>, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for a Ticketing integration, otherwise <see langword="false"/>.</returns>
    bool TryGetTicketingSettings(Integration integration, [NotNullWhen(true)] out ITicketingSettings? settings);

    /// <summary>
    /// Reads the settings stored in the provided integration's <see cref="Integration.Settings"/> property
    /// </summary>
    /// <param name="integration">The <see cref="Integration"/> to read settings from.</param>
    /// <typeparam name="T">The type to deserialize the settings to. This type must be able to deserialize a blank JSON object.</typeparam>
    /// <returns>The settings provided in the <see cref="Integration"/>, or <c>null</c> if no settings are present yet.</returns>
    T ReadSettings<T>(Integration integration)
        where T : class, IIntegrationSettings;

    /// <summary>
    /// Updates the settings in the provided integration's <see cref="Integration.Settings"/> property with a serialized version of the provided <paramref name="settings"/>.
    /// </summary>
    /// <param name="integration">The <see cref="Integration"/> to write settings to.</param>
    /// <param name="settings">The settings object to use to update the integration settings.</param>
    /// <typeparam name="T">The settings type.</typeparam>
    /// <returns>The settings.</returns>
    Task<T> SaveSettingsAsync<T>(Integration integration, T settings)
        where T : class, IIntegrationSettings;
}
