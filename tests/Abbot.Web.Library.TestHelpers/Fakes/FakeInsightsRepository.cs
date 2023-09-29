using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;
using Serious.Filters;
using Serious.TestHelpers;

namespace Abbot.Common.TestHelpers.Fakes;

public class FakeInsightsRepository : IInsightsRepository
{
    IEnumerable<ConversationVolumePeriod>? _rollups;
    IReadOnlyList<RoomConversationVolume>? _roomConversationVolumes;

    public Task<InsightsStats> GetSummaryStatsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DateRangeSelector dateRangeSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        Organization = organization;
        RoomSelector = roomSelector;
        DateRangeSelector = dateRangeSelector;
        TagSelector = tagSelector;
        return Task.FromResult(new InsightsStats(1, 23, 15, 20, 23));
    }

    public Task<IReadOnlyList<ConversationVolumePeriod>> GetConversationVolumeRollupsAsync(Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        Organization = organization;
        RoomSelector = roomSelector;
        DatePeriodSelector = datePeriodSelector;
        TagSelector = tagSelector;
        return Task.FromResult(_rollups?.ToReadOnlyList() ?? Array.Empty<ConversationVolumePeriod>());
    }

    public Task<IReadOnlyList<RoomConversationVolume>> GetConversationVolumeByRoomAsync(Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        Organization = organization;
        RoomSelector = roomSelector;
        DatePeriodSelector = datePeriodSelector;
        TagSelector = tagSelector;

        var result = _roomConversationVolumes?.ToReadOnlyList()
            ?? Array.Empty<RoomConversationVolume>();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ResponderConversationVolume>> GetConversationVolumeByResponderAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        throw new NotImplementedException();
    }

    public void SetConversationVolumeByRoom(params RoomConversationVolume[] roomConversationVolumes)
    {
        _roomConversationVolumes = roomConversationVolumes;
    }

    public async Task<IReadOnlyList<RoomOption>> GetRoomFilterList(Organization organization)
    {
        return await Task.FromResult(new[]
        {
            new RoomOption("Room 1", "C000001"),
            new RoomOption("Room 2", "C000002"),
        }.ToReadOnlyList());
    }

    public Task<Conversation?> GetFirstConversationAsync(Organization organization, RoomSelector roomSelector)
    {
        Organization = organization;
        RoomSelector = roomSelector;

        return Task.FromResult<Conversation?>(new Conversation { Created = Dates.ParseUtc("2020-01-01T00:00:00Z") });
    }

    public Task<Dictionary<ConversationLinkType, int>> GetCreatedTicketCountsAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<TagFrequency>> GetTagFrequencyAsync(
        Organization organization,
        RoomSelector roomSelector,
        DatePeriodSelector datePeriodSelector,
        TagSelector tagSelector,
        FilterList filter)
    {
        throw new NotImplementedException();
    }

    public void SetConversationVolumeRollups(IEnumerable<ConversationVolumePeriod> rollups)
    {
        _rollups = rollups;
    }

    public Organization? Organization { get; private set; }

    public RoomSelector? RoomSelector { get; private set; }

    public TagSelector? TagSelector { get; private set; }

    public DatePeriodSelector? DatePeriodSelector { get; private set; }

    public DateRangeSelector? DateRangeSelector { get; private set; }
}
