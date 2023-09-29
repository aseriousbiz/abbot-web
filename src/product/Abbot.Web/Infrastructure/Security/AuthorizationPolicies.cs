using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Serious.Abbot.Security;

namespace Serious.Abbot.Infrastructure.Security;

public static class AuthorizationPolicies
{
    public const string Default = RequireAnyRole;

    public const string SkillRunnerApi = nameof(SkillRunnerApi);
    public const string PublicApi = nameof(PublicApi);
    public const string RequireAuthenticated = nameof(RequireAuthenticated);
    public const string RequireAnyRole = nameof(RequireAnyRole);
    public const string RequireAgentRole = nameof(RequireAgentRole);
    public const string RequireStaffRole = nameof(RequireStaffRole);
    public const string RequireAdministratorRole = nameof(RequireAdministratorRole);
    public const string RequireStaffOrLocalDev = nameof(RequireStaffOrLocalDev);
    public const string CanManageConversations = nameof(CanManageConversations);

    public static void AddPolicies(this IServiceCollection services)
    {
        services.AddStaffMode();
        services.AddAuthorization(options => {
            options.AddPolicy(RequireAuthenticated,
                policy => policy.RequireAuthenticatedUser());

            options.AddPolicy(RequireAnyRole,
                policy => policy.RequireRole(Roles.Agent, Roles.Administrator));

            options.AddPolicy(RequireAgentRole,
                policy => policy.RequireRole(Roles.Agent));

            options.AddPolicy(RequireAdministratorRole,
                policy => policy.RequireRole(Roles.Administrator));

            options.AddPolicy(CanManageConversations,
                policy => policy.RequireRole(Roles.Agent, Roles.Administrator));

            options.AddPolicy(RequireStaffRole,
                policy => policy.AddRequirements(new StaffModeRequirement("/staff/enable")));

            options.AddPolicy(RequireStaffOrLocalDev,
                policy => {
                    policy.AddRequirements(new StaffModeRequirement("/staff/enable")
                    {
                        AllowedInDevelopmentEnvironment = true
                    });
                });

            options.AddPolicy(PublicApi,
                policy => policy
                    .AddAuthenticationSchemes(AuthenticationConfig.ApiKeyScheme)
                    .RequireAuthenticatedUser()
                    .RequireRole(Roles.Agent, Roles.Administrator, Roles.Staff));

            options.AddPolicy(SkillRunnerApi,
                policy => policy
                    .AddAuthenticationSchemes(AuthenticationConfig.SkillTokenScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => {
                        if (context.Resource is not HttpContext httpContext)
                        {
                            // This policy can only be used by web requests
                            return false;
                        }

                        var skillId = httpContext.GetRouteValue("skillId")?.ToString()
                            ?? httpContext.Request.Query["skillId"].ToString();
                        if (skillId is not { Length: > 0 })
                        {
                            // This policy requires a 'skillId' route parameter for the skill ID.
                            return false;
                        }

                        var expectedClaim = $"skillId={skillId}";
                        var audClaim = context.User.FindFirst("aud");

                        // The audience claim must match the skill ID in the route parameters.
                        return audClaim?.Value == expectedClaim;
                    }));

            // Default policy when [Authorize] attribute is applied but no policy is specified
            options.DefaultPolicy = options.GetPolicy(Default).Require();
        });
    }

    public static void ConfigureRazorPagesAuthorization(RazorPagesOptions options)
    {
        // Unfortunately, when there are multiple authorization policies on a page, they combine with AND rather than overriding.
        // The exception is AllowAnonymous, which _is not_ an authorization policy but a special override that tells the Authz system to ignore failures.
        // So to be cautious, we're setting RequireAuthenticated here because it's safe to combine that with _any_ of our other policies.
        // Most of the pages/folders below override this default by adding additional policies,
        // but for any that don't, we still want to limit them to authenticated users (EXCEPT if they allow anonymous)
        options.Conventions.AuthorizeFolder("/", RequireAuthenticated);

        // Anonymous access allowed.
        options.Conventions.AllowAnonymousToFolder("/Packages");
        options.Conventions.AllowAnonymousToFolder("/Status");
        options.Conventions.AllowAnonymousToPage("/NonProduction");

        // Role not required
        options.Conventions.AuthorizePage("/Index", RequireAuthenticated); // Redirects to Account if no role
        options.Conventions.AuthorizeFolder("/Settings/Account", RequireAuthenticated);

        // Any Role.
        options.Conventions.AuthorizeFolder("/Activity", RequireAnyRole);

        // Conversation Management.
        options.Conventions.AuthorizeFolder("/Conversations", CanManageConversations);
        options.Conventions.AuthorizeFolder("/Settings/Rooms", CanManageConversations);

        // Agents only
        options.Conventions.AuthorizeFolder("/Announcements", RequireAgentRole);
        options.Conventions.AuthorizeFolder("/Insights", RequireAgentRole);
        options.Conventions.AuthorizeFolder("/Lists", RequireAnyRole);
        options.Conventions.AuthorizeFolder("/Skills", RequireAnyRole);

        // Administrator access
        options.Conventions.AuthorizeFolder("/Settings/Organization", RequireAdministratorRole);

        // Staff-only access
        options.Conventions.AuthorizeFolder("/Debug", RequireStaffOrLocalDev);
        options.Conventions.AuthorizeFolder("/Staff", RequireStaffRole);
    }
}
