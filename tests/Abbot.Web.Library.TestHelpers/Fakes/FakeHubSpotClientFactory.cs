using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Cryptography;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeHubSpotClientFactory : IHubSpotClientFactory
{
    public IDictionary<string, IHubSpotClient> Clients { get; } = new Dictionary<string, IHubSpotClient>();
    public IDictionary<string, IHubSpotFormsClient> HubSpotFormsClients { get; } = new Dictionary<string, IHubSpotFormsClient>();

    public Task<SecretString> GetOrRenewAccessTokenAsync(Integration integration, HubSpotSettings settings)
    {
        throw new System.NotImplementedException();
    }

    public IHubSpotOAuthClient CreateOAuthClient()
    {
        throw new NotImplementedException();
    }

    public async Task<IHubSpotClient> CreateClientAsync(Integration integration, HubSpotSettings settings)
    {
        var accessToken = settings.AccessToken.Require();
        return ClientFor(accessToken.Reveal());
    }

    public async Task<IHubSpotFormsClient> CreateFormsClientAsync(Integration integration, HubSpotSettings settings)
    {
        var accessToken = settings.AccessToken.Require();
        return HubSpotFormsClientFor(accessToken.Reveal());
    }

    public IHubSpotClient ClientFor(string? accessToken)
    {
        // We accept null for convenience only.
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        if (!Clients.TryGetValue(accessToken, out var client))
        {
            client = Substitute.For<IHubSpotClient>();
            Clients.Add(accessToken, client);
        }

        return client;
    }

    public IHubSpotFormsClient HubSpotFormsClientFor(string? accessToken)
    {
        // We accept null for convenience only.
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        if (!HubSpotFormsClients.TryGetValue(accessToken, out var client))
        {
            client = Substitute.For<IHubSpotFormsClient>();
            HubSpotFormsClients.Add(accessToken, client);
        }

        return client;
    }
}
