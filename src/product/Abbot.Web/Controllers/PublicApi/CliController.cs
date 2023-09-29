using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Controllers.PublicApi;

/// <summary>
/// The API for the abbot command line interface.
/// </summary>
[ApiController]
[AbbotApiHost]
[Route("api/cli")]
[Authorize(Policy = AuthorizationPolicies.PublicApi)]
public class CliController : SkillRunControllerBase // NOTE TO FUTURE SELF: We probably shouldn't inherit from this. Instead, refactor it and inject it.
{
    // The version of abbot-cli required to use this API.
    public static readonly Version MinimumClientVersion = new Version(0, 3, 0, 0);

    static readonly ILogger<CliController> Log = ApplicationLoggerFactory.CreateLogger<CliController>();
    readonly ISkillRepository _skillRepository;
    readonly IPermissionRepository _permissions;

    public CliController(
        ISkillRepository skillRepository,
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        IPermissionRepository permissions,
        ICachingCompilerService cachingCompilerService,
        ISkillRunnerClient skillRunnerClient,
        IUrlGenerator urlGenerator)
        : base(skillRunnerClient, userRepository, roomRepository, cachingCompilerService, permissions, urlGenerator)
    {
        _skillRepository = skillRepository;
        _permissions = permissions;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        static bool IsValidClientVersion(string? clientVersionHeader)
        {
            return Version.TryParse(clientVersionHeader, out var clientVersion)
                   && clientVersion >= MinimumClientVersion;
        }

        var clientVersionHeader = context.HttpContext.Request.Headers["X-Client-Version"].SingleOrDefault() ?? "0.0.0";
        if (!IsValidClientVersion(clientVersionHeader))
        {
            context.Result = Problem(
                $"The Abbot CLI client ({clientVersionHeader}) you are using is outdated. Please visit https://github.com/aseriousbiz/abbot-cli/releases and use the latest release",
                AbbotSchema.GetProblemUri("outdated_client"),
                StatusCodes.Status403Forbidden,
                "Client version outdated",
                AbbotSchema.GetProblemUri("outdated_client"));
            return;
        }

        // Sets CurrentMember
        base.OnActionExecuting(context);

        var organization = CurrentMember.Organization;
        if (!organization.ApiEnabled)
        {
            var adminSettingsUrl = UrlGenerator.OrganizationSettingsPage();
            context.Result = Problem(
                $"The organization {organization.Name} ({organization.PlatformId}) has the API disabled. This setting can be changed by an Administrator at {adminSettingsUrl}",
                AbbotSchema.GetProblemUri("api_disabled"),
                StatusCodes.Status403Forbidden,
                "API Disabled",
                AbbotSchema.GetProblemUri("api_disabled"));
        }
    }

    /// <summary>
    /// Retrieves the org and user associated with the API Key passed in the authorization header.
    /// </summary>
    [HttpGet]
    [Route("status")]
    public IActionResult GetStatusAsync()
    {
        Log.MethodEntered(typeof(CliController), nameof(GetStatusAsync), null);
        var (user, organization) = CurrentMember;

        var response = new StatusGetResponse
        {
            Organization = new OrganizationGetResponse
            {
                Name = organization.Name,
                Domain = organization.Domain,
                PlatformId = organization.PlatformId,
                Platform = organization.PlatformType.ToString()
            },
            User = new UserGetResponse
            {
                Name = user.DisplayName,
                PlatformUserId = user.PlatformUserId
            }
        };
        return new ObjectResult(response);
    }

    /// <summary>
    /// Retrieves a list of user-defined skills.
    /// </summary>
    [HttpGet]
    [Route("list")]
    public async Task<IActionResult> ListSkillsAsync(
        SkillOrderBy orderBy = SkillOrderBy.Name,
        OrderDirection direction = OrderDirection.Ascending,
        bool includeDisable = false)
    {
        Log.MethodEntered(typeof(CliController), nameof(ListSkillsAsync), null);

        var queryable = _skillRepository.GetSkillListQueryable(CurrentMember.Organization);

        queryable = (orderBy, direction) switch
        {
            (SkillOrderBy.Name, OrderDirection.Ascending) => queryable.OrderBy(s => s.Name),
            (SkillOrderBy.Name, OrderDirection.Descending) => queryable.OrderByDescending(s => s.Name),
            (SkillOrderBy.Created, OrderDirection.Ascending) => queryable.OrderBy(s => s.Created),
            (SkillOrderBy.Created, OrderDirection.Descending) => queryable.OrderByDescending(s => s.Created),
            (SkillOrderBy.Modified, OrderDirection.Ascending) => queryable.OrderBy(s => s.Modified),
            (SkillOrderBy.Modified, OrderDirection.Descending) => queryable.OrderByDescending(s => s.Modified),
            _ => throw new InvalidOperationException("Unexpected order by, direction combo")
        };

        queryable = queryable.Where(s => includeDisable || s.Enabled);

        var skills = await queryable.ToListAsync();

        var response = new SkillListResponse
        {
            OrderBy = orderBy,
            OrderDirection = direction,
            Results = skills.Select(ToSkillGetResponse).ToList()
        };
        return new ObjectResult(response);
    }

    [HttpGet]
    [Route("{skill}")]
    public async Task<IActionResult> GetSkillAsync(string skill)
    {
        Log.SkillMethodEntered(typeof(CliController), nameof(GetSkillAsync), null, skill);

        var retrieved = await _skillRepository.GetAsync(skill, CurrentMember.Organization);
        if (retrieved is null || retrieved.IsDeleted)
        {
            return NotFound();
        }

        return new ObjectResult(ToSkillGetResponse(retrieved));
    }

    [HttpPut]
    [Route("{skill}")]
    public async Task<IActionResult> UpdateSkillAsync(string skill, [FromBody] SkillUpdateRequest updateRequest)
    {
        Log.SkillMethodEntered(typeof(CliController), nameof(UpdateSkillAsync), null, skill);

        var retrieved = await _skillRepository.GetAsync(skill, CurrentMember.Organization);
        if (retrieved is null || retrieved.IsDeleted)
        {
            return NotFound();
        }

        if (!await _permissions.CanEditAsync(CurrentMember, retrieved))
        {
            return Forbid();
        }

        // Concurrency check.
        var retrievedHash = retrieved.Code.ComputeSHA1Hash();
        if (retrievedHash != updateRequest.PreviousCodeHash)
        {
            var detail = $@"The skill cannot be updated because it was changed since it was last retrieved

    Last Modified:  {retrieved.Modified}
    Modified By:    {retrieved.ModifiedBy.DisplayName} ({retrieved.ModifiedBy.PlatformUserId})
";
            var problemResult = Problem(detail,
                Request.Path,
                StatusCodes.Status409Conflict,
                "Update Conflict");
            if (problemResult.Value is ProblemDetails problemDetails)
            {
                problemDetails.Extensions.Add("Conflict", new ConflictInfo
                {
                    Modified = retrieved.Modified,
                    ModifiedBy = ToUserGetResponse(retrieved.ModifiedBy)
                });
            }

            return problemResult;
        }

        var updateModel = new SkillUpdateModel
        {
            Code = updateRequest.Code
        };

        var result = await _skillRepository.UpdateAsync(retrieved, updateModel, CurrentMember.User);

        return new ObjectResult(new SkillUpdateResponse
        {
            Updated = result,
            NewCodeHash = retrieved.Code.ComputeSHA1Hash()
        });
    }

    [HttpPost]
    [Route("{skill}/run")]
    public async Task<IActionResult> RunSkillAsync(string skill, [FromBody] SkillRunRequest runRequest)
    {
        Log.SkillMethodEntered(typeof(CliController), nameof(RunSkillAsync), null, skill);

        var retrieved = await _skillRepository.GetAsync(skill, CurrentMember.Organization);
        if (retrieved is null)
        {
            return NotFound();
        }

        return await SendSkillRunRequestAsync(
            retrieved,
            CurrentMember,
            runRequest,
            new PlatformRoom("abbot-cli", "abbot-cli"));
    }

    /// <summary>
    /// Runs the deployed version of the skill, ignoring the `Code` parameter of <see cref="SkillRunRequest" />.
    /// </summary>
    /// <param name="skill">The skill to run.</param>
    /// <param name="runRequest">The request to run the skill.</param>
    /// <returns></returns>
    [HttpPost]
    [Route("{skill}/deployed/run")]
    public async Task<IActionResult> RunDeployedSkillAsync(string skill, [FromBody] SkillRunRequest runRequest)
    {
        Log.SkillMethodEntered(typeof(CliController), nameof(RunDeployedSkillAsync), null, skill);

        var retrieved = await _skillRepository.GetAsync(skill, CurrentMember.Organization);
        if (retrieved is null)
        {
            return NotFound();
        }

        runRequest.Code = retrieved.Code;
        return await SendSkillRunRequestAsync(
            retrieved,
            CurrentMember,
            runRequest,
            new PlatformRoom("abbot-cli", "abbot-cli"));
    }

    static SkillGetResponse ToSkillGetResponse(Skill retrieved)
    {
        return new()
        {
            Name = retrieved.Name,
            Code = retrieved.Code,
            CodeHash = retrieved.Code.ComputeSHA1Hash(),
            Language = retrieved.Language,
            Enabled = retrieved.Enabled,
            LastModified = retrieved.Modified,
            LastModifiedBy = ToUserGetResponse(retrieved.ModifiedBy)
        };
    }

    static UserGetResponse ToUserGetResponse(User user)
    {
        return new UserGetResponse
        {
            Name = user.DisplayName,
            PlatformUserId = user.PlatformUserId
        };
    }
}
