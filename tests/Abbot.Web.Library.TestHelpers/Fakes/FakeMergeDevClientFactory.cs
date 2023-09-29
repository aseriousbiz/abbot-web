using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NSubstitute;
using Serious;
using Serious.Abbot.Integrations.MergeDev;
using Serious.Cryptography;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeMergeDevClientFactory : IMergeDevClientFactory
{
    public IDictionary<string, IMergeDevClient> Clients { get; } = new Dictionary<string, IMergeDevClient>();

    public HttpRequestMessage ApplyAuthorization(HttpRequestMessage request, string? accountToken = null)
    {
        return request;
    }

    public IMergeDevClient CreateClient(TicketingSettings settings)
    {
        var accessToken = settings.AccessToken.Require();
        return ClientFor(accessToken.Reveal());
    }

    public IMergeDevClient ClientFor(string? accessToken)
    {
        // We accept null for convenience only.
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        if (!Clients.TryGetValue(accessToken, out var client))
        {
            client = Substitute.For<IMergeDevClient>();
            Clients.Add(accessToken, client);
        }

        return client;
    }
}
