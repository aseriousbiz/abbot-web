using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Logging;

namespace Serious.Abbot.Controllers.InternalApi;

/// <summary>
/// API for the skill editor to call in order to invoke the skill code without saving it first.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.RequireAnyRole)]
[ApiController]
[AbbotWebHost]
// non-GET APIs that use cookie auth _must_ use an Anti-Froggery token to prevent CSRF attacks
[AutoValidateAntiforgeryToken]
[Area(InternalApiControllerBase.Area)]
[ApiExplorerSettings(GroupName = "internal")]
[Route("api/internal/skills")] // "api/skills"
public class SkillEditorController : SkillRunControllerBase
{
    static readonly ILogger<SkillEditorController> Log = ApplicationLoggerFactory.CreateLogger<SkillEditorController>();

    readonly ISkillRepository _skillRepository;

    public SkillEditorController(
        ISkillRunnerClient skillRunnerClient,
        ISkillRepository skillRepository,
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        ICachingCompilerService cachingCompilerService,
        IPermissionRepository permissions,
        IUrlGenerator urlGenerator)
        : base(skillRunnerClient, userRepository, roomRepository, cachingCompilerService, permissions, urlGenerator)
    {
        _skillRepository = skillRepository;
    }

    [HttpGet("invoke")]
    public IActionResult Index()
    {
        return Content($"Abbot Web /api/internal/skills/invoke  v{ReflectionExtensions.GetAssemblyVersion()} is up and running!");
    }

    [IgnoreAntiforgeryToken] // TODO: We really _should_ be providing an anti-forgery token...
    [HttpPost("{id:int}/invoke")]
    public async Task<IActionResult> InvokeAsync(Id<Skill> id, [FromBody] SkillRunRequest skillRunRequest)
    {
        Log.SkillMethodEntered(typeof(SkillEditorController), nameof(InvokeAsync), id, skillRunRequest.Name);

        var member = CurrentMember;

        if (id == default)
        {
            // Skill must be saved before it's invoked.
            return NotFound();
        }

        var skill = await _skillRepository.GetByIdAsync(id);
        if (skill is null)
        {
            return NotFound();
        }

        var result = await SendSkillRunRequestAsync(
            skill,
            member,
            skillRunRequest,
            new PlatformRoom("skill-editor", "Skill Editor"));

        // Translate to what the Skill Editor client (browser) expects.
        return result switch
        {
            ObjectResult { StatusCode: StatusCodes.Status200OK or null, Value: SkillRunResponse response }
                => new ObjectResult(string.Join('\n', response.Replies ?? Array.Empty<string>())),
            ObjectResult { StatusCode: StatusCodes.Status403Forbidden }
                => new ObjectResult($"I'm afraid I can't do that, {member.FormatMention()}. " +
                                    $"`@abbot who can {skill.Name}` to find out who can change permissions " +
                                    "for this skill."),
            ObjectResult { StatusCode: StatusCodes.Status500InternalServerError, Value: CompilerErrorResponse compilerErrorResponse }
                => new ObjectResult(compilerErrorResponse.Errors) { StatusCode = StatusCodes.Status500InternalServerError },
            ObjectResult { StatusCode: StatusCodes.Status500InternalServerError, Value: RuntimeErrorResponse runtimeErrorResponse }
                => new ObjectResult(runtimeErrorResponse.Errors) { StatusCode = StatusCodes.Status500InternalServerError },
            _ => result
        };
    }
}
