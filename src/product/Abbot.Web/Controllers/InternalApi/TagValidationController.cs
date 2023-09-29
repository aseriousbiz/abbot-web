using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Controllers.InternalApi;

[ApiController]
[AbbotWebHost]
[Route("api/internal/tags")]
public class TagValidationController : InternalApiControllerBase
{
    readonly ITagRepository _tagRepository;

    /// <summary>
    /// Constructs a new instance of the <see cref="TagValidationController"/> class.
    /// </summary>
    /// <param name="tagRepository">The <see cref="ITagRepository"/>.</param>
    public TagValidationController(ITagRepository tagRepository)
    {
        _tagRepository = tagRepository;
    }

    /// <summary>
    /// Validates that a name is unique for the tag and organization.
    /// </summary>
    /// <param name="newTagName">Name of the tag to test.</param>
    /// <returns>A JSON result with true or a string representing the error.</returns>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateAsync(string newTagName)
    {
        var existing = await _tagRepository.GetTagByNameAsync(newTagName, Organization);

        return existing is null
            ? Json(true)
            : Json($"A tag with the name \"{existing.Name}\" already exists.");
    }
}

