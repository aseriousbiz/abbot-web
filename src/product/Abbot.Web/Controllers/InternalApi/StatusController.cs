using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serious.Abbot.Configuration;

namespace Serious.Abbot.Controllers.InternalApi;

// Can't use InternalApiControllerBase because that enforces GetCurrentMember.
[ApiController]
[Area(InternalApiControllerBase.Area)]
[ApiExplorerSettings(GroupName = "internal")]
[Route("api/internal/status")]
public class StatusController : ControllerBase
{
    readonly IOptions<AbbotOptions> _abbotOptions;

    public StatusController(IOptions<AbbotOptions> abbotOptions)
    {
        _abbotOptions = abbotOptions;
    }

    // GET /
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Index()
    {
        // Yeah, this is some pretty simple auth.
        // If we add more endpoints, we can get fancier here.
        if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return NotFound();
        }

        if (authHeader.Count != 1 || authHeader[0] != $"Bearer {_abbotOptions.Value.MonitoringToken}")
        {
            return NotFound();
        }

        // This is an anonymous endpoint, so we should ensure we're comfortable disclosing all the information we put here.
        return Ok(new {
            Status = "OK",
            Version = Program.BuildMetadata.InformationalVersion,
            Commit = Program.BuildMetadata.CommitId,
            Runtime = RuntimeInformation.FrameworkDescription,
            Platform = RuntimeInformation.RuntimeIdentifier,
        });
    }
}
