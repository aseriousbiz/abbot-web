using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Entities;
using Serious.Abbot.Models.Api;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// Internal API for searching emoji.
/// </summary>
[Route("api/internal/emoji")]
public class EmojiController : InternalApiControllerBase
{
    readonly IEmojiLookup _emojiLookup;

    public EmojiController(IEmojiLookup emojiLookup)
    {
        _emojiLookup = emojiLookup;
    }

    /// <summary>
    /// Returns all emoji matching <paramref name="q"/>.
    /// </summary>
    /// <param name="q">The substring to request.</param>
    /// <param name="limit">The number of results to return.</param>
    [HttpGet("search")]
    public async Task<IActionResult> RenderAsync([FromQuery] string? q, [FromQuery] int limit = 10)
    {
        if (!Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            return Problem("No API token configured.");
        }

        var results = await _emojiLookup.SearchAsync(q, Array.Empty<string>(), limit, apiToken);
        return Json(results);
    }

    /// <summary>
    /// Returns all emoji matching <paramref name="q"/>.
    /// </summary>
    /// <param name="q">The substring to request.</param>
    /// <param name="currentValue">The current value(s).</param>
    /// <param name="limit">The number of results to return.</param>
    [HttpGet("typeahead")]
    public async Task<IActionResult> GetEmojiAsync([FromQuery] string? q, [FromQuery] string[] currentValue, [FromQuery] int limit = 10)
    {
        if (!Organization.TryGetUnprotectedApiToken(out var apiToken))
        {
            return Problem("No API token configured.");
        }

        var results = await _emojiLookup.SearchAsync(q, currentValue, limit, apiToken);
        return Json(results.Select(TypeAheadResponseModel.Create).ToList());
    }
}

