using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Integrations.GitHub;

public abstract record GitHubLink : IntegrationLink
{
    public override IntegrationType IntegrationType => IntegrationType.GitHub;
}

public record GitHubIssueLink(string Owner, string Repo, int Number) : GitHubLink
{
    static readonly Regex IssueWebUrlParser =
        new(@"https://(www\.)?github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/issues/(?<number>\d+)", RegexOptions.Compiled);
    static readonly Regex IssueApiUrlParser =
        new(@"https://api\.github\.com/repos/(?<owner>[^/]+)/(?<repo>[^/]+)/issues/(?<number>\d+)", RegexOptions.Compiled);

    public override Uri ApiUrl => new($"https://api.github.com/repos/{Owner}/{Repo}/issues/{Number}");
    public override Uri WebUrl => new($"https://github.com/{Owner}/{Repo}/issues/{Number}");

    public static GitHubIssueLink? Parse(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (IssueWebUrlParser.Match(input) is { Success: true } webMatch)
        {
            return new GitHubIssueLink(
                webMatch.Groups["owner"].Value,
                webMatch.Groups["repo"].Value,
                int.Parse(webMatch.Groups["number"].Value, CultureInfo.InvariantCulture));
        }

        if (IssueApiUrlParser.Match(input) is { Success: true } apiMatch)
        {
            return new GitHubIssueLink(
                apiMatch.Groups["owner"].Value,
                apiMatch.Groups["repo"].Value,
                int.Parse(apiMatch.Groups["number"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }
}
