using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Controllers;

[ApiController]
[AbbotWebHost]
[Authorize(Policy = AuthorizationPolicies.SkillRunnerApi)]
[Route("api/skills/{skillId}")]
public abstract class SkillRunnerApiControllerBase : Controller
{
    static readonly ILogger<SkillRunnerApiControllerBase> Log =
        ApplicationLoggerFactory.CreateLogger<SkillRunnerApiControllerBase>();

    protected Member Member { get; private set; } = null!;

    protected Skill Skill { get; private set; } = null!;

    protected Organization Organization => Skill.Organization;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (User is not
            { Identity: { IsAuthenticated: true, AuthenticationType: AuthenticationConfig.SkillTokenScheme } })
        {
            // Pft. No token. *yeet*
            context.Result = new NotFoundResult();
            return;
        }

        if (User.FindAll(ApiTokenFactory.IsAbbotClaim).ToList() is { Count: > 0 } abbotClaims)
        {
            // V2 token
            // Load up the relevant entities from the claims
            if (await LoadFromV2ClaimsAsync(abbotClaims, context) is { } result)
            {
                context.Result = result;
                return;
            }
        }
        else
        {
            context.Result = new NotFoundResult();
            return;
        }

        // Enter Logging scopes

        // If we have a skill, use it's organization for the logging scope (in case the skill is being invoked by a member from another organization)
        // If we don't have a skill, it's because we're handling a request from an unsaved skill, so using the Member's org is fine.
        using var orgScope = Log.BeginOrganizationScope(Skill.Organization);
        using var memberScope = Log.BeginMemberScope(Member);
        using var skillScope = Log.BeginSkillScope(Skill);

        await base.OnActionExecutionAsync(context, next);
    }

    async Task<IActionResult?> LoadFromV2ClaimsAsync(IReadOnlyList<Claim> abbotClaims, ActionContext context)
    {
        // OMG SERVICE LOCATOR!
        // Ok, so hear me out. The intent here is that the subclass shouldn't be beholden to the dependencies of this base class.
        // If the derived class wants a user repository, it needs to depend on it explicitly.
        // Using service locator here also means that derived classes don't have to pass constructor arguments along.
        var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var skillRepository = context.HttpContext.RequestServices.GetRequiredService<ISkillRepository>();

        var memberId = Id<Member>.Parse(abbotClaims.Single(c => c.Type == ApiTokenFactory.MemberIdClaimType).Value, CultureInfo.InvariantCulture);
        var skillId = Id<Skill>.Parse(abbotClaims.Single(c => c.Type == ApiTokenFactory.SkillIdClaimType).Value, CultureInfo.InvariantCulture);

        var member = await userRepository.GetMemberByIdAsync(memberId);
        if (member is null)
        {
            return new NotFoundResult();
        }

        Member = member;

        var skill = await skillRepository.GetByIdAsync(skillId);
        if (skill is null)
        {
            return new NotFoundResult();
        }
        Skill = skill;

        return null;
    }
}
