using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Serious.Abbot.Entities;
using Serious.Abbot.Integrations.Zendesk.Models;

namespace Serious.Abbot.Integrations.Zendesk;

public abstract record ZendeskLink(string Subdomain) : IntegrationLink
{
    public override IntegrationType IntegrationType => IntegrationType.Zendesk;
}

public record ZendeskTicketLink(string Subdomain, long TicketId) : ZendeskLink(Subdomain)
{
    static readonly Regex TicketApiUrlParser =
        new(@"https://(?<subdomain>.+?)\.zendesk\.com/api/v2/tickets/(?<id>\d+)\.json",
            RegexOptions.Compiled);
    static readonly Regex TicketWebUrlParser =
        new(@"https://(?<subdomain>.+?)\.zendesk\.com/agent/tickets/(?<id>\d+)",
            RegexOptions.Compiled);

    public override Uri ApiUrl => new($"https://{Subdomain}.zendesk.com/api/v2/tickets/{TicketId}.json");
    public override Uri WebUrl => new($"https://{Subdomain}.zendesk.com/agent/tickets/{TicketId}");

    public string? Status { get; set; }

    public static ZendeskTicketLink? Parse(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (TicketApiUrlParser.Match(input) is { Success: true } apiMatch)
        {
            return new ZendeskTicketLink(apiMatch.Groups["subdomain"].Value,
                long.Parse(apiMatch.Groups["id"].Value, CultureInfo.InvariantCulture));
        }

        if (TicketWebUrlParser.Match(input) is { Success: true } webMatch)
        {
            return new ZendeskTicketLink(webMatch.Groups["subdomain"].Value,
                long.Parse(webMatch.Groups["id"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    public override string ToString() => ApiUrl.ToString();
}

public record ZendeskUserLink(string Subdomain, long UserId) : ZendeskLink(Subdomain)
{
    static readonly Regex UserApiUrlParser =
        new(@"https://(?<subdomain>.+?)\.zendesk\.com/api/v2/users/(?<id>\d+)\.json",
            RegexOptions.Compiled);
    static readonly Regex UserWebUrlParser =
        new(@"https://(?<subdomain>.+?)\.zendesk\.com/agent/users/(?<id>\d+)",
            RegexOptions.Compiled);

    public override Uri ApiUrl => new($"https://{Subdomain}.zendesk.com/api/v2/users/{UserId}.json");
    public override Uri WebUrl => new($"https://{Subdomain}.zendesk.com/agent/users/{UserId}");

    public static ZendeskUserLink? Parse(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (UserApiUrlParser.Match(input) is { Success: true } apiMatch)
        {
            return new(apiMatch.Groups["subdomain"].Value,
                long.Parse(apiMatch.Groups["id"].Value, CultureInfo.InvariantCulture));
        }

        if (UserWebUrlParser.Match(input) is { Success: true } webMatch)
        {
            return new(webMatch.Groups["subdomain"].Value,
                long.Parse(webMatch.Groups["id"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    public override string ToString() => ApiUrl.ToString();
}

/// <summary>
/// Extra user metadata about the Zendesk user.
/// </summary>
public class ZendeskUserMetadata
{
    public ZendeskUserMetadata()
    {
    }

    /// <summary>
    /// Extra user metadata about the Zendesk user.
    /// </summary>
    /// <param name="role">Their Zendesk role.</param>
    /// <param name="subdomain">Their Zendesk subdomain.</param>
    public ZendeskUserMetadata(string? role, string? subdomain)
    {
        Role = role;
        Subdomain = subdomain;
    }

    /// <summary>Their Zendesk role.</summary>
    public string? Role { get; set; }

    /// <summary>Their Zendesk subdomain.</summary>
    public string? Subdomain { get; set; }

    /// <summary>Indicates if this user is a "Facade" user.</summary>
    public bool IsFacade { get; set; }

    /// <summary>Any known organization IDs.</summary>
    [Obsolete("Do not use! This comes from a misinterpretation of Zendesk organization logic!")]
    public IList<long> KnownOrganizationIds { get; set; } = new List<long>();

    public static ZendeskUserMetadata FromUser(ZendeskUser zendeskUser, ZendeskUserLink link, bool isFacade)
    {
        return new(zendeskUser.Role, link.Subdomain)
        {
            IsFacade = isFacade,
        };
    }
}

public record ZendeskOrganizationLink(string Subdomain, long OrganizationId) : ZendeskLink(Subdomain)
{
    static readonly Regex OrganizationApiUrlParser =
        new(@"https://(?<subdomain>.+?)\.zendesk\.com/api/v2/organizations/(?<id>\d+)\.json",
            RegexOptions.Compiled);
    static readonly Regex OrganizationWebUrlParser =
        new(@"https://(?<subdomain>.+?)\.zendesk\.com/agent/organizations/(?<id>\d+)",
            RegexOptions.Compiled);

    public override Uri ApiUrl => new($"https://{Subdomain}.zendesk.com/api/v2/organizations/{OrganizationId}.json");
    public override Uri WebUrl => new($"https://{Subdomain}.zendesk.com/agent/organizations/{OrganizationId}");

    public static ZendeskOrganizationLink? Parse(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (OrganizationApiUrlParser.Match(input) is { Success: true } apiMatch)
        {
            return new(apiMatch.Groups["subdomain"].Value,
                long.Parse(apiMatch.Groups["id"].Value, CultureInfo.InvariantCulture));
        }

        if (OrganizationWebUrlParser.Match(input) is { Success: true } webMatch)
        {
            return new(webMatch.Groups["subdomain"].Value,
                long.Parse(webMatch.Groups["id"].Value, CultureInfo.InvariantCulture));
        }

        return null;
    }

    public override string ToString() => ApiUrl.ToString();
}
