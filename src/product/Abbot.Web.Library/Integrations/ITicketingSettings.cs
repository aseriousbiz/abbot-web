using System.Linq;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Serialization;
using Serious.Logging;
using Serious.Slack;

namespace Serious.Abbot.Integrations;

public record TicketingIntegration(Integration Integration, ITicketingSettings Settings);

/// <summary>
/// Represents settings for a Ticketing Integration.
/// </summary>
/// <remarks>
/// Does not implement <see cref="IIntegrationSettings"/>
/// because the static abstract <see cref="IIntegrationSettings.IntegrationType"/>
/// prevents use as a generic type argument.
/// https://github.com/dotnet/csharplang/issues/5955
/// https://github.com/dotnet/csharplang/blob/main/proposals/csharp-11.0/static-abstracts-in-interfaces.md#interfaces-as-type-arguments
/// </remarks>
public interface ITicketingSettings
{
    static readonly ILogger<ITicketingSettings> Log =
        ApplicationLoggerFactory.CreateLogger<ITicketingSettings>();

    /// <summary>
    /// The display name of the Integration.
    /// </summary>
    string IntegrationName { get; }

    /// <summary>
    /// The slug for the Integration.
    /// </summary>
    string IntegrationSlug { get; }

    /// <summary>
    /// The Id of the message where we report and update the status of the ticket.
    /// </summary>
    string? StatusMessageId => null;

    /// <summary>
    /// The Integration has credentials to interact with its API.
    /// </summary>
    bool HasApiCredentials { get; }

    string AnalyticsSlug => IntegrationSlug;

    /// <summary>
    /// The <see cref="ConversationLinkType"/> for this Integration.
    /// </summary>
    ConversationLinkType ConversationLinkType { get; }

    /// <summary>
    /// Finds a link for this Ticketing Integration in <paramref name="conversation"/>.
    /// </summary>
    /// <param name="conversation">The <see cref="Conversation"/>.</param>
    /// <param name="integration">This settings' <see cref="Integration"/>.</param>
    /// <returns>The matching <see cref="ConversationLink"/>, otherwise <see langword="null"/>.</returns>
    ConversationLink? FindLink(Conversation? conversation, Integration integration) =>
        conversation?.Links.SingleOrDefault(l => l.LinkType == ConversationLinkType);

    /// <summary>
    /// Read a Ticket <see cref="IntegrationLink"/> from a <see cref="ConversationLink"/>.
    /// </summary>
    /// <param name="conversationLink">The <see cref="ConversationLink"/>.</param>
    /// <returns>The <see cref="IntegrationLink"/>, or <see langword="null"/> if empty or invalid.</returns>
    IntegrationLink? GetTicketLink(ConversationLink? conversationLink);

    /// <summary>
    /// The <see cref="RoomLinkType"/> for this Integration.
    /// </summary>
    RoomLinkType RoomLinkType => RoomLinkType.Unknown;

    /// <summary>
    /// Finds a link for this Ticketing Integration in <paramref name="room"/>.
    /// </summary>
    /// <param name="room">The <see cref="Room"/>.</param>
    /// <param name="integration">This settings' <see cref="Integration"/>.</param>
    /// <returns>The matching <see cref="IntegrationRoomLink"/>, otherwise <see langword="null"/>.</returns>
    IntegrationRoomLink? FindLink(Room room, Integration integration)
    {
        var links = room.Links.Where(rl => rl.LinkType == RoomLinkType).ToList();
        if (links.Count > 1)
        {
            Log.RoomHasMultipleIntegrationLinks(RoomLinkType);
        }

        return links is [{ DisplayName: var displayName } roomLink, ..]
            && GetRoomIntegrationLink(roomLink) is { } integrationLink
            ? new IntegrationRoomLink(displayName, integrationLink)
            : null;
    }

    /// <summary>
    /// Read a Room <see cref="IntegrationLink"/> from a <see cref="RoomLink"/>.
    /// </summary>
    /// <param name="roomLink">The <see cref="RoomLink"/>.</param>
    /// <returns></returns>
    IntegrationLink? GetRoomIntegrationLink(RoomLink? roomLink) => null;
}

public static class TicketingExtensions
{
    public static IntegrationLink? GetTicketLink(this Conversation conversation, TicketingIntegration ticketing)
    {
        return ticketing.Settings.FindLink(conversation, ticketing.Integration) is { } cl
            && ticketing.Settings.GetTicketLink(cl) is { } il
            ? il :
            null;
    }

    public static IntegrationRoomLink? GetIntegrationLink(this Room room, TicketingIntegration ticketing)
    {
        return ticketing.Settings.FindLink(room, ticketing.Integration);
    }

    public static string? ToSlackLink(this IntegrationRoomLink? link, bool hasPermissions = true) =>
        hasPermissions && link is { DisplayName: { Length: > 0 } name, Link.WebUrl: { } webUrl }
            ? new Hyperlink(webUrl, name).ToString()
            : link?.DisplayName;
}

/// <summary>
/// Base record for links to Integration tickets.
/// </summary>
public abstract record IntegrationLink : JsonSettings
{
    /// <summary>
    /// The link's <see cref="IntegrationType"/>.
    /// </summary>
    public abstract IntegrationType IntegrationType { get; }

    /// <summary>
    /// The Integration API URL.
    /// </summary>
    public abstract Uri ApiUrl { get; }

    /// <summary>
    /// The Integration web URL.
    /// </summary>
    public abstract Uri? WebUrl { get; }
}

public record IntegrationRoomLink(
    string? DisplayName = null,
    IntegrationLink? Link = null);

static partial class TicketSettingsLoggingExtensions
{
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Room has multiple links of type {RoomLinkType}")]
    public static partial void
        RoomHasMultipleIntegrationLinks(this ILogger<ITicketingSettings> logger, RoomLinkType roomLinkType);
}
