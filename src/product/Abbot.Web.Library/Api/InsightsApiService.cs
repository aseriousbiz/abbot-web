using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using NodaTime;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;
using Serious.Filters;
using Serious.Logging;

namespace Serious.Abbot.Api;

public class InsightsApiService
{
    static readonly ILogger<InsightsApiService> Log = ApplicationLoggerFactory.CreateLogger<InsightsApiService>();

    public static readonly DateTimeZone DefaultTimeZone =
        DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles") ?? DateTimeZone.Utc;

    readonly IInsightsRepository _insightsRepository;
    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;
    readonly IConversationRepository _conversationRepository;
    readonly IClock _clock;

    public InsightsApiService(
        IInsightsRepository insightsRepository,
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        IConversationRepository conversationRepository,
        IClock clock)
    {
        _insightsRepository = insightsRepository;
        _userRepository = userRepository;
        _roomRepository = roomRepository;
        _conversationRepository = conversationRepository;
        _clock = clock;
    }

    public async Task<IReadOnlyList<TagFrequency>?> GetTagFrequencyAsync(
        Member actor,
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var roomSelector = await GetRoomSelector(filter, actor);
        if (roomSelector is null)
        {
            return null;
        }

        return await GetTagFrequencyAsync(actor, range, roomSelector, tz, tag, q);
    }

    public async Task<IReadOnlyList<TagFrequency>?> GetTagFrequencyAsync(
        Member actor,
        DateRangeOption range,
        RoomSelector roomSelector,
        string? tz = null,
        string? tag = null,
        FilterList q = default)
    {
        var datePeriodSelector = GetDatePeriodSelector(range, tz, actor);
        var tagFilter = TagSelector.Create(tag);

        return await _insightsRepository.GetTagFrequencyAsync(
            actor.Organization,
            roomSelector,
            datePeriodSelector,
            tagFilter,
            q);
    }

    public async Task<InsightsSummaryInfo?> GetSummaryAsync(
        Member actor,
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var roomSelector = await GetRoomSelector(filter, actor);
        if (roomSelector is null)
        {
            return null;
        }

        return await GetSummaryAsync(actor, range, roomSelector, tz, tag, q);
    }

    public async Task<InsightsSummaryInfo> GetSummaryAsync(
        Member actor,
        DateRangeOption range,
        RoomSelector roomSelector,
        string? tz = null,
        string? tag = null,
        FilterList q = default)
    {
        var tagFilter = TagSelector.Create(tag);

        var datePeriodSelector = GetDatePeriodSelector(range, tz, actor);
        var conversationStats = await _insightsRepository.GetSummaryStatsAsync(
            actor.Organization,
            roomSelector,
            datePeriodSelector,
            tagFilter,
            q);

        var ticketsOpened = await _insightsRepository.GetCreatedTicketCountsAsync(
            actor.Organization,
            roomSelector,
            datePeriodSelector,
            tagFilter,
            q);

        return new InsightsSummaryInfo(
            conversationStats.WentOverdueCount,
            conversationStats.NeededAttentionCount,
            conversationStats.RespondedCount,
            conversationStats.OpenedCount,
            ticketsOpened.Sum(t => t.Value),
            conversationStats.OpenedConversationsRoomCount); // For now, we just lump all tickets together.
    }

    public async Task<ConversationVolumeResponseModel?> GetVolumeAsync(
        Member actor,
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var roomSelector = await GetRoomSelector(filter, actor);
        if (roomSelector is null)
        {
            return null;
        }
        var (days, _, timezone) = GetDatePeriodSelector(range, tz, actor);


        // Limit to the number of days for which we have data for the org.
        var firstConversation = await _insightsRepository.GetFirstConversationAsync(actor.Organization, roomSelector);
        if (firstConversation is null)
        {
            return new ConversationVolumeResponseModel(timezone.Id, Array.Empty<ConversationVolumePeriod>());
        }

        if (days > 7)
        {
            // Get the number of days for which we have data just in case we can't fill the graph.
            // The `+1` accounts for the fact that we look at the end of the current day.
            var numberOfDays = _clock.UtcNow.Subtract(firstConversation.Created).Days + 1;
            // If we only have one day of data, we'll still show 7 days. Otherwise shit gets weird.
            var numberOfDaysOfDataWeHave = Math.Max(7, numberOfDays);
            // However, if we don't have enough data to fill the data range, we'll truncate what we show.
            days = Math.Min(numberOfDaysOfDataWeHave, days);
        }

        var datePeriodSelector = new DatePeriodSelector(days, _clock, timezone);
        var tagFilter = TagSelector.Create(tag);
        var dailyInsights = await _insightsRepository.GetConversationVolumeRollupsAsync(
            actor.Organization,
            roomSelector,
            datePeriodSelector,
            tagFilter,
            q);

        return new ConversationVolumeResponseModel(timezone.Id, dailyInsights);
    }

    public async Task<InsightRoomConversationVolumeViewModel?> GetConversationVolumeByRoomAsync(
        Member actor,
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var roomSelector = await GetRoomSelector(filter, actor);
        if (roomSelector is null)
        {
            return null;
        }
        var datePeriodSelector = GetDatePeriodSelector(range, tz, actor);
        var tagFilter = TagSelector.Create(tag);
        var roomVolumes = await _insightsRepository.GetConversationVolumeByRoomAsync(
            actor.Organization,
            roomSelector,
            datePeriodSelector,
            tagFilter,
            q);

        var totalConversations = roomVolumes.Sum(v => v.OpenConversationCount);

        return InsightRoomConversationVolumeViewModel.FromRoomVolumes(roomVolumes, totalConversations);
    }

    public async Task<InsightResponderConversationVolumeViewModel?> GetConversationVolumeByUserAsync(
        Member actor,
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var roomSelector = await GetRoomSelector(filter, actor);
        if (roomSelector is null)
        {
            return null;
        }
        var datePeriodSelector = GetDatePeriodSelector(range, tz, actor);
        var tagFilter = TagSelector.Create(tag);
        var responderVolumes = await _insightsRepository.GetConversationVolumeByResponderAsync(
            actor.Organization,
            roomSelector,
            datePeriodSelector,
            tagFilter,
            q);

        return InsightResponderConversationVolumeViewModel.FromResponderVolumes(responderVolumes);
    }

    public async Task<TrendsResponseModel> GetTrendsAsync(
        Member actor,
        DateRangeOption? range,
        string? tz = null,
        string? filter = null,
        string? tag = null,
        FilterList q = default)
    {
        var roomSelector = await GetRoomSelector(filter, actor)
                           ?? new AssignedRoomsSelector(actor.Id, RoomRole.FirstResponder);

        return await GetTrendsAsync(actor, range, roomSelector, tz, tag, q);
    }

    public async Task<TrendsResponseModel> GetTrendsAsync(
        Member actor,
        DateRangeOption? range,
        RoomSelector roomSelector,
        string? tz = null,
        string? tag = null,
        FilterList q = default)
    {
        var datePeriodSelector = GetDatePeriodSelector(range ?? DateRangeOption.Week, tz, actor);
        var tagFilter = TagSelector.Create(tag);

        var rollUps = await _conversationRepository.GetDailyRollupsAsync(
            roomSelector,
            datePeriodSelector,
            tagFilter,
            actor.Organization,
            q);

        // Compute the top-level roll-ups
        var averages = new TrendsSummary(
            rollUps.Average(r => r.AverageTimeToFirstResponse?.TotalSeconds ?? 0),
            rollUps.Average(r => r.AverageTimeToResponse?.TotalSeconds ?? 0),
            rollUps.Average(r => r.AverageTimeToFirstResponseDuringCoverage?.TotalSeconds ?? 0),
            rollUps.Average(r => r.AverageTimeToResponseDuringCoverage?.TotalSeconds ?? 0),
            rollUps.Average(r => r.AverageTimeToClose?.TotalSeconds ?? 0),
            rollUps.Sum(r => r.NewConversations));

        return new TrendsResponseModel(
            datePeriodSelector.Timezone.Id,
            averages,
            rollUps.Select(r => new TrendsDay(
                r.Start.Date,
                r.AverageTimeToFirstResponse?.TotalSeconds,
                r.AverageTimeToResponse?.TotalSeconds,
                r.AverageTimeToFirstResponseDuringCoverage?.TotalSeconds,
                r.AverageTimeToResponseDuringCoverage?.TotalSeconds,
                r.AverageTimeToClose?.TotalSeconds,
                r.PercentWithinTarget,
                r.NewConversations)).ToList());
    }

    public DatePeriodSelector GetDatePeriodSelector(DateRangeOption range, string? tz, Member actor)
    {
        var timezone = GetTimezone(tz, actor);
        var days = GetDaysFromRange(range);
        return new DatePeriodSelector(days, _clock, timezone);
    }

    static int GetDaysFromRange(DateRangeOption range)
    {
        return range switch
        {
            DateRangeOption.Week => 7,
            DateRangeOption.Month => 30,
            DateRangeOption.Year => 90,
            _ => throw new UnreachableException()
        };
    }

    static DateTimeZone GetTimezone(string? tz, Member actor)
    {
        tz ??= actor.TimeZoneId;

        // Compute roll-ups
        var timeZone = tz is { Length: > 0 } && DateTimeZoneProviders.Tzdb.GetZoneOrNull(tz) is { } tzi
            ? tzi
            : DefaultTimeZone;
        return timeZone;
    }

    async Task<RoomSelector?> GetRoomSelector(string? filter, Member currentMember)
    {
        var organization = currentMember.Organization;
        var roomSelector = filter switch
        {
            InsightsRoomFilter.All when currentMember.IsAdministrator() => RoomSelector.AllRooms,
            InsightsRoomFilter.Yours => new ResponderRoomSelector(currentMember),
            not null when currentMember.IsAdministrator() => InsightsRoomFilter.GetAddressType(filter) switch
            {
                ChatAddressType.Room => await GetSpecificRoomSelector(filter, organization),
                ChatAddressType.User => await GetAssignedRoomSelector(filter, organization),
                var addressType => LogUnexpectedFilter(addressType),
            },
            _ => null
        };
        return roomSelector;

        ResponderRoomSelector? LogUnexpectedFilter(ChatAddressType? addressType)
        {
            Log.UnexpectedFilter(filter, addressType);
            return null;
        }
    }

    async Task<RoomSelector?> GetSpecificRoomSelector(string platformRoomId, Organization organization)
    {
        var room = await _roomRepository.GetRoomByPlatformRoomIdAsync(platformRoomId, organization);
        return room is null ? null : new SpecificRoomsSelector(room.Id);
    }

    async Task<ResponderRoomSelector?> GetAssignedRoomSelector(string platformUserId, Organization organization)
    {
        var member = await _userRepository.GetByPlatformUserIdAsync(platformUserId, organization);
        return member is null ? null : new ResponderRoomSelector(member);
    }
}

public enum DateRangeOption
{
    [Display(Name = "Past 7 days")]
    Week,

    [Display(Name = "Past 30 days")]
    Month,

    [Display(Name = "Past 365 days")]
    Year
}

static partial class ChartsApiControllerBaseLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Unexpected filter '{Filter}' or address type '{ChatAddressType}'.")]
    public static partial void UnexpectedFilter(
        this ILogger<InsightsApiService> logger,
        string? filter,
        ChatAddressType? chatAddressType);
}
