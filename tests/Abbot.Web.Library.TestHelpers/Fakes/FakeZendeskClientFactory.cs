using System;
using System.Collections.Generic;
using System.Diagnostics;
using Serious;
using Serious.Abbot.Integrations.Zendesk;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeZendeskClientFactory : IZendeskClientFactory
{
    public IDictionary<string, FakeZendeskClient> Clients { get; } = new Dictionary<string, FakeZendeskClient>();

    public IZendeskClient CreateClient(ZendeskSettings settings)
    {
        var client = ClientFor(settings.Subdomain.Require());
        client.Settings = settings;
        return client;
    }

    public IZendeskOAuthClient CreateOAuthClient(string subdomain)
    {
        throw new System.NotImplementedException();
    }

    public FakeZendeskClient ClientFor(string? subdomain)
    {
        // We accept null for convenience only.
        ArgumentException.ThrowIfNullOrEmpty(subdomain);

        if (!Clients.TryGetValue(subdomain, out var client))
        {
            client = new FakeZendeskClient();
            Clients[subdomain] = client;
        }
        return client;
    }
}
