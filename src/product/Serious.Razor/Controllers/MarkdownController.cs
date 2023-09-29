using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serious.Razor.Components.Services;

namespace Serious.Razor.Controllers;

[ApiController]
[Route("api/markdown")]
[Authorize]
public class MarkdownController : Controller
{
    readonly IMarkdownService _markdownService;

    public MarkdownController(IMarkdownService markdownService)
    {
        _markdownService = markdownService;
    }

    [HttpPost]
    public ContentResult Post([FromBody] string content)
    {
        var result = _markdownService.Render(content);
        return Content(result, "text/html");
    }
}
