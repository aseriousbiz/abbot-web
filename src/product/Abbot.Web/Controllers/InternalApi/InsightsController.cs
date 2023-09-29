using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Api;
using Serious.Abbot.Models.Api;
using Serious.Abbot.Repositories;
using Serious.Filters;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Internal API for Insights data.
/// </summary>
[Route("api/internal/insights")]
public class InsightsController : InternalApiControllerBase
{
    readonly InsightsApiService _insightsApiService;

    public InsightsController(InsightsApiService insightsApiService)
    {
        _insightsApiService = insightsApiService;
    }

    [HttpGet("tags")]
    public async Task<IActionResult> GetTagsAsync(
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var tagFrequencies = await _insightsApiService.GetTagFrequencyAsync(CurrentMember, range, tz, filter, tag, q);
        if (tagFrequencies is null)
        {
            return NotFound();
        }

        return PartialView("Insights/_Tags", tagFrequencies);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummaryAsync(
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var summary = await _insightsApiService.GetSummaryAsync(CurrentMember, range, tz, filter, tag, q);
        if (summary is null)
        {
            return NotFound();
        }

        return PartialView("Insights/_Summary", summary);
    }

    [HttpGet("volume")]
    [ProducesResponseType(typeof(ConversationVolumeResponseModel), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVolumeAsync(
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var volume = await _insightsApiService.GetVolumeAsync(CurrentMember, range, tz, filter, tag, q);
        if (volume is null)
        {
            return NotFound();
        }

        return Ok(volume);
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetConversationVolumeByRoomAsync(
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var volume = await _insightsApiService.GetConversationVolumeByRoomAsync(
            CurrentMember,
            range,
            tz,
            filter,
            tag,
            q);
        if (volume is null)
        {
            return NotFound();
        }
        return PartialView("Insights/_Rooms", volume);
    }

    [HttpGet("responders")]
    public async Task<IActionResult> GetConversationVolumeByUserAsync(
        DateRangeOption range = DateRangeOption.Week,
        string? tz = null,
        string filter = InsightsRoomFilter.Yours,
        string? tag = null,
        FilterList q = default)
    {
        var volume = await _insightsApiService.GetConversationVolumeByUserAsync(
            CurrentMember,
            range,
            tz,
            filter,
            tag,
            q);
        if (volume is null)
        {
            return NotFound();
        }
        return PartialView("Insights/_Responders", volume);
    }
}
