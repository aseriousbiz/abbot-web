using System.Collections.Generic;
using Serious.Abbot.Entities;
using Serious.Abbot.Models.Api;
using Serious.Filters;

namespace Serious.Abbot.Repositories;

/// <summary>
/// Provides methods to query data for the Insights page.
/// </summary>
public interface IInsightsRepository
{
    /// <summary>
    /// Retrieves stats for the Insights page for all rooms in the organization.
    /// </summary>
    /// <param name="organization">The organization to query.</param>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    /// <param name="dateRangeSelector">Used to filter the date range for the stats.</param>
    /// <param name="tagSelector">Only include conversations tagged with the tag specified by the selector.</param>
    /// <param name="filter">Filters to apply to the query such as customer or segment.</param>
    /// <returns>Returns overall summary stats.</returns>
    Task<InsightsStats> GetSummaryStatsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DateRangeSelector dateRangeSelector,
        TagSelector tagSelector,
        FilterList filter);

    /// <summary>
    /// Retrieves conversation volume stats for the Insights graphs.
    /// </summary>
    /// <param name="organization">The organization to query.</param>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    /// <param name="datePeriodSelector">The period to roll-up stats for.</param>
    /// <param name="tagSelector">Only include conversations tagged with the tag specified by the selector.</param>
    /// <param name="filter">Filters to apply to the query such as customer or segment.</param>
    /// <returns>Returns an ordered roll-up of conversation volume for the date period.</returns>
    Task<IReadOnlyList<ConversationVolumePeriod>> GetConversationVolumeRollupsAsync(Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter);

    /// <summary>
    /// Retrieves conversation volume stats per room for the Insights graphs.
    /// </summary>
    /// <param name="organization">The organization to query.</param>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    /// <param name="datePeriodSelector">The period to roll-up stats for.</param>
    /// <param name="tagSelector">Only include conversations tagged with the tag specified by the selector.</param>
    /// <param name="filter">Filters to apply to the query such as customer or segment.</param>
    /// <returns>The set of rooms matching the <paramref name="roomSelector"/> for the date range and the room counts.</returns>
    Task<IReadOnlyList<RoomConversationVolume>> GetConversationVolumeByRoomAsync(Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter);

    /// <summary>
    /// Retrieves conversation volume stats per responder for the Insights graphs.
    /// </summary>
    /// <param name="organization">The organization to query.</param>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    /// <param name="datePeriodSelector">The period to roll-up stats for.</param>
    /// <param name="tagSelector">Only include conversations tagged with the tag specified by the selector.</param>
    /// <param name="filter">Filters to apply to the query such as customer or segment.</param>
    /// <returns>The conversation volume per first-responder for the set of rooms matching the <paramref name="roomSelector"/> and the date range and the room counts.</returns>
    Task<IReadOnlyList<ResponderConversationVolume>> GetConversationVolumeByResponderAsync(Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter);

    /// <summary>
    /// Retrieves the list of rooms for the Insights page room filter. This is any room that has conversations
    /// enabled OR has ever had a conversation.
    /// </summary>
    /// <param name="organization">The organization.</param>
    Task<IReadOnlyList<RoomOption>> GetRoomFilterList(Organization organization);

    /// <summary>
    /// Gets the first conversation for the given organization and room selector.
    /// </summary>
    /// <param name="organization">The organization to query.</param>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    Task<Conversation?> GetFirstConversationAsync(Organization organization, RoomSelector roomSelector);

    /// <summary>
    /// Returns the number of tickets opened in the selected rooms and date period.
    /// </summary>
    /// <param name="organization">The organization.</param>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    /// <param name="datePeriodSelector">The period to roll-up stats for.</param>
    /// <param name="tagSelector">Only include conversations tagged with the tag specified by the selector.</param>
    /// <param name="filter">Filters to apply to the query such as customer or segment.</param>
    Task<Dictionary<ConversationLinkType, int>> GetCreatedTicketCountsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter);

    /// <summary>
    /// For each tag applied to a conversation that meets the query, this returns the tag and the count of
    /// conversations the tag has been applied to in order from most to least.
    /// </summary>
    /// <param name="organization">The organization to query.</param>
    /// <param name="roomSelector">Filters on the set of rooms to include.</param>
    /// <param name="datePeriodSelector">The period to roll-up stats for.</param>
    /// <param name="tagSelector">Only include conversations tagged with the tag specified by the selector.</param>
    /// <param name="filter">Filters to apply to the query such as customer or segment.</param>
    Task<IReadOnlyList<TagFrequency>> GetTagFrequencyAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter);
}

/// <summary>
/// Represents a room option for the Insights page room filter.
/// </summary>
/// <param name="Name">The name of the room.</param>
/// <param name="PlatformRoomId">The platform-specific Id of the room.</param>
public record RoomOption(string Name, string PlatformRoomId);

/// <summary>
/// These are the special filter options for the room filter. A filter can also be a specific
/// channel id or user id (platform IDs).
/// </summary>
public static class InsightsRoomFilter
{
    /// <summary>
    /// Return results for all rooms in the organization.
    /// </summary>
    public const string All = "all";

    /// <summary>
    /// Return results for all rooms where the current user is a first-responder.
    /// </summary>
    public const string Yours = "yours";

    /// <summary>
    /// Get the <see cref="ChatAddressType"/> for the given filter
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    public static ChatAddressType? GetAddressType(string filter) =>
        SlackIdUtility.GetChatAddressTypeFromSlackId(filter);
}

/// <summary>
/// Contains information about a room, the number of open conversations, and the set of first responders for that room.
/// </summary>
/// <param name="Room">The room.</param>
/// <param name="OpenConversationCount">The number of open conversations.</param>
public record RoomConversationVolume(Room Room, int OpenConversationCount);

/// <summary>
/// Contains conversation volume information about a member.
/// </summary>
/// <param name="Member">The member.</param>
/// <param name="OpenConversationCount">The number of open </param>
public record ResponderConversationVolume(Member Member, int OpenConversationCount);

/// <summary>
/// The frequency of an individual tag.
/// </summary>
/// <param name="Tag">The tag.</param>
/// <param name="Count">How many conversations it's been applied to.</param>
public record TagFrequency(Tag Tag, int Count);
