using System.Globalization;
using System.Text.RegularExpressions;
using Serious.Abbot.Entities;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Integrations.HubSpot;

public abstract record HubSpotLink(long HubId) : IntegrationLink
{
    public override IntegrationType IntegrationType => IntegrationType.HubSpot;
}

/// <summary>
/// Contains information about the linked HubSpot ticket such as the Hub and Ticket ID.
/// </summary>
/// <param name="HubId">The Id of the Hub.</param>
/// <param name="TicketId">The Id of the ticket.</param>
public record HubSpotTicketLink(long HubId, string TicketId) : HubSpotLink(HubId)
{
    static readonly Regex TicketWebUrlParser =
        new(@"https://app.hubspot.com/contacts/(?<hubId>\d+)/ticket/(?<id>\d+)",
            RegexOptions.Compiled);

    public override Uri ApiUrl => new($"https://api.hubapi.com/crm/v3/objects/tickets/{TicketId}");
    public override Uri WebUrl => new($"https://app.hubspot.com/contacts/{HubId}/ticket/{TicketId}");

    /// <summary>
    /// If the HubSpot ticket has an associated HubSpot Conversation thread, this will be the Id of that thread.
    /// </summary>
    public long? ThreadId { get; init; }

    /// <summary>
    /// Creates a <see cref="HubSpotTicketLink"/> from a <see cref="ConversationLink"/>.
    /// </summary>
    /// <param name="link">The <see cref="ConversationLink"/> to HubSpot.</param>
    /// <returns></returns>
    public static HubSpotTicketLink? FromConversationLink(ConversationLink link)
    {
        var ticketLink = Parse(link.ExternalId);
        if (ticketLink is null)
        {
            return null;
        }

        return ticketLink with
        {
            ThreadId = link.Settings is { } settingsJson
                ? JsonSettings.FromJson<HubSpotLinkSettings>(settingsJson)?.ThreadId
                : null
        };
    }

    /// <summary>
    /// Parses out a HubId and TicketId from an input string.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    static HubSpotTicketLink? Parse(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (TicketWebUrlParser.Match(input) is { Success: true } webMatch)
        {
            return new HubSpotTicketLink(
                long.Parse(webMatch.Groups["hubId"].Value, CultureInfo.InvariantCulture),
                webMatch.Groups["id"].Value);
        }

        return null;
    }

    // Use WebUrl because it round-trips HubId
    public override string ToString() => WebUrl.ToString();
}

/// <summary>
/// HubSpot settings for a conversation link.
/// </summary>
/// <param name="ThreadId">The Id of the thread linked to the conversation.</param>
public record HubSpotLinkSettings(long ThreadId) : JsonSettings;

public record HubSpotContactLink(long HubId, string ContactId) : HubSpotLink(HubId)
{
    static readonly Regex ContactWebUrlParser =
        new(@"https://app.hubspot.com/contacts/(?<hubId>\d+)/contact/(?<id>\d+)",
            RegexOptions.Compiled);

    public override Uri ApiUrl => new($"https://api.hubapi.com/crm/v3/objects/companies/{ContactId}");
    public override Uri WebUrl => new($"https://app.hubspot.com/contacts/{HubId}/contact/{ContactId}");

    public static HubSpotContactLink? Parse(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (ContactWebUrlParser.Match(input) is { Success: true } apiMatch)
        {
            return new(long.Parse(apiMatch.Groups["hubId"].Value, CultureInfo.InvariantCulture),
                apiMatch.Groups["id"].Value);
        }

        return null;
    }

    // Use WebUrl because it round-trips HubId
    public override string ToString() => WebUrl.ToString();
}

/// <summary>
/// Extra user metadata about the HubSpot contact.
/// </summary>
public class HubSpotContactMetadata
{
    // Maybe useful someday?
}

public record HubSpotCompanyLink(long HubId, string CompanyId) : HubSpotLink(HubId)
{
    static readonly Regex CompanyWebUrlParser =
        new(@"https://app.hubspot.com/contacts/(?<hubId>\d+)/company/(?<id>\d+)",
            RegexOptions.Compiled);

    public override Uri ApiUrl => new($"https://api.hubapi.com/crm/v3/objects/companies/{CompanyId}");
    public override Uri WebUrl => new($"https://app.hubspot.com/contacts/{HubId}/company/{CompanyId}");

    public static HubSpotCompanyLink? Parse(string? input)
    {
        if (input is null)
        {
            return null;
        }

        if (CompanyWebUrlParser.Match(input) is { Success: true } apiMatch)
        {
            return new(long.Parse(apiMatch.Groups["hubId"].Value, CultureInfo.InvariantCulture),
                apiMatch.Groups["id"].Value);
        }

        return null;
    }

    // Use WebUrl because it round-trips HubId
    public override string ToString() => WebUrl.ToString();
}
