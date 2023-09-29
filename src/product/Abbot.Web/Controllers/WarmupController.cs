using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Controllers;

[ApiController]
[AllowAnonymous]
[AbbotWebHost]
public class WarmupController : Controller
{
    readonly AbbotContext _db;
    readonly IConfiguration _configuration;

    public WarmupController(AbbotContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [HttpGet("/warmup")]
    public async Task<IActionResult> GetAsync()
    {
        var orgCount = await _db.Organizations.CountAsync();

        // Check if there's a deployment ID we should report
        var deploymentId = _configuration["Abbot:DeploymentId"];

        return Ok(new {
            Commit = Program.BuildMetadata.CommitId,
            Version = Program.BuildMetadata.InformationalVersion,
            DeploymentId = deploymentId,
        });
    }

    [HttpGet("/warmup/pid")]
    public async Task<IActionResult> GetPidAsync()
    {
        return Ok(Environment.ProcessId);
    }

    [HttpGet("/warmup/deployment-id")]
    public async Task<IActionResult> GetDeploymentIdAsync()
    {
        var orgCount = await _db.Organizations.CountAsync();

        // Check if there's a deployment ID we should report
        var deploymentId = _configuration["Abbot:DeploymentId"];

        return Ok(deploymentId);
    }
}
