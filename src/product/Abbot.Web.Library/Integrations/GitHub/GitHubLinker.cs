using System.Collections.Generic;
using System.Net;
using Octokit;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Integrations.GitHub;

public class GitHubLinker : ITicketLinker<GitHubSettings, Issue>
{
    readonly IGitHubClientFactory _clientFactory;
    readonly IConversationRepository _conversationRepository;
    readonly IClock _clock;

    public GitHubLinker(
        IGitHubClientFactory clientFactory,
        IConversationRepository conversationRepository,
        IClock clock)
    {
        _clientFactory = clientFactory;
        _conversationRepository = conversationRepository;
        _clock = clock;
    }

    public async Task<Issue?> CreateTicketAsync(
        Integration integration,
        GitHubSettings settings,
        IReadOnlyDictionary<string, object?> properties,
        Conversation conversation,
        Member actor)
    {
        var repository = settings.DefaultRepository;
        if (properties.TryGetValue("repository", out var formRepository))
        {
            repository = formRepository as string ?? "";
        }
        else if (repository is null or "")
        {
            throw new TicketConfigurationException("Default Repository is not configured.");
        }

        // Splitting into at most 3 so we can ensure there are only two segments
        if (repository.Split('/', 3) is not [var owner, var repo]
            || owner is ""
            || repo is "")
        {
            throw new TicketConfigurationException($"Repository '{repository}' is not valid; expected 'owner/repo'.");
        }

        var client = await _clientFactory.CreateInstallationClientAsync(integration, settings);
        var body = (string?)properties["body"]; // null is fine, but non-string is unexpected

        if (properties.TryGetValue("footer", out var footer)
            && footer is string { Length: > 0 })
        {
            body += "\n\n" + footer;
        }

        var newIssue = new NewIssue(properties["title"].Require<string>())
        {
            Body = body,
        };
        return await client.Issue.Create(owner, repo, newIssue);
    }

    public TicketError ParseException(Exception ex) =>
        ex switch
        {
            ApiException apiException =>
                apiException.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => new(TicketErrorReason.Unauthorized),
                    _ => new(
                        TicketErrorReason.ApiError,
                        null, /* TODO: What's safe to show users? */
                        apiException.ApiError.ToString()),
                },
            _ => new(TicketErrorReason.Unknown),
        };

    public async Task<ConversationLink?> CreateConversationLinkAsync(
        Integration integration,
        GitHubSettings settings,
        Issue ticket,
        Conversation conversation,
        Member actor)
    {
        // Link the conversation to that ticket
        return await _conversationRepository.CreateLinkAsync(
            conversation,
            ConversationLinkType.GitHubIssue,
            ticket.Url,
            actor,
            _clock.UtcNow);
    }
}
