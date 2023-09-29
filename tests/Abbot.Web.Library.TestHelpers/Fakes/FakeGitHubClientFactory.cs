using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Octokit;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.GitHub;
using Serious.Cryptography;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeGitHubClientFactory : IGitHubClientFactory
{
    public IDictionary<string, IGitHubClient> Clients { get; } = new Dictionary<string, IGitHubClient>();

    public HttpRequestMessage ApplyAuthorization(HttpRequestMessage request, string apiToken)
    {
        throw new NotImplementedException();
    }

    public IGitHubClient CreateAnonymousClient() => ClientFor("GitHub:Anonymous");

    public IGitHubClient CreateAppClient() => ClientFor("GitHub");

    public Task<SecretString> GetOrRenewAccessTokenAsync(Integration integration, GitHubSettings settings)
    {
        throw new NotImplementedException();
    }

    public async Task<IGitHubClient> CreateInstallationClientAsync(Integration integration, GitHubSettings settings) =>
        ClientFor(settings);

    public IGitHubClient ClientFor(GitHubSettings settings) =>
        ClientFor($"GitHub:{settings.InstallationId}");

    public IGitHubClient ClientFor(string? accessToken)
    {
        // We accept null for convenience only.
        ArgumentException.ThrowIfNullOrEmpty(accessToken);

        if (!Clients.TryGetValue(accessToken, out var client))
        {
            client = Substitute.For<IGitHubClient>();
            //client.Issues.Returns(Substitute.For<IIssuesClient>());
            Clients.Add(accessToken, client);
        }

        return client;
    }

    static int _nextNumber = (int)(100 + DateTime.UtcNow.Ticks % 200);

    public Issue CreateFakeIssue(string repository, NewIssue newIssue) =>
        new FakeGitHubIssue(repository, Interlocked.Increment(ref _nextNumber), newIssue.Title, newIssue.Body);

    class FakeGitHubIssue : Issue
    {
        public FakeGitHubIssue(string repository, int number, string title, string? body)
        {
            Number = number;
            Url = $"https://api.github.com/repos/{repository}/issues/{number}";
            HtmlUrl = $"https://github.com/{repository}/issues/{number}";
            Title = title;
            Body = body;
        }
    }
}
