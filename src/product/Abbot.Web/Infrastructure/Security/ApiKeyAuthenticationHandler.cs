using System;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.Abbot.Security;

namespace Serious.Abbot.Infrastructure.Security;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    readonly IUserRepository _userRepository;
    readonly IRoleManager _roleManager;
    readonly ProblemDetailsFactory _problemDetailsFactory;
    readonly IOptions<MvcNewtonsoftJsonOptions> _mvcJsonOptions;
    public static readonly Version MinimumClientVersion = new(0, 2, 2, 0);

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IUserRepository userRepository,
        IRoleManager roleManager,
        ProblemDetailsFactory problemDetailsFactory,
        IOptions<MvcNewtonsoftJsonOptions> mvcJsonOptions) : base(options, logger, encoder, clock)
    {
        _userRepository = userRepository;
        _roleManager = roleManager;
        _problemDetailsFactory = problemDetailsFactory;
        _mvcJsonOptions = mvcJsonOptions;
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var problem = _problemDetailsFactory.CreateProblemDetails(
            Context,
            StatusCodes.Status401Unauthorized,
            "Authentication Failed",
            AbbotSchema.GetProblemUri("auth_failed"),
            "Authentication failed. Either the 'Authorization' headed was missing, invalid, or didn't include a valid API Key. Go to your Account Settings to generate a valid API Key.",
            instance: AbbotSchema.GetProblemUri("auth_failed"));

        Context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        Context.Response.ContentType = "application/problem+json";
        var problemJson = JsonConvert.SerializeObject(problem, _mvcJsonOptions.Value.SerializerSettings);
        await Context.Response.WriteAsync(problemJson);
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var request = Context.Request;
        var authHeader = request.Headers["Authorization"].SingleOrDefault();
        if (authHeader is null)
        {
            // No token
            return AuthenticateResult.NoResult();
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
        {
            // Bad token
            return AuthenticateResult.NoResult();
        }

        var token = authHeader["Bearer ".Length..];

        if (token is null or { Length: 0 })
        {
            // No token, or bad token.
            return AuthenticateResult.NoResult();
        }

        var member = await _userRepository.GetMemberByApiToken(token);
        if (member is null)
        {
            return AuthenticateResult.Fail("Invalid token");
        }

        var claims = new Claim[]
        {
            new(AbbotSchema.GetSchemaUri("abbot_member_id"), $"{member.Id}")
        };

        var id = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(new[] { id });
        _roleManager.SyncRolesToPrincipal(member, principal);
        Context.SetCurrentMember(member);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
