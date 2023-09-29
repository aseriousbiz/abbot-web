using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Serious.Abbot.Filters;
using Serious.Abbot.Security;

namespace Serious.Abbot.Infrastructure.Security;

public static class AuthenticationConfig
{
    public const string SkillTokenScheme = "SkillToken";
    public const string ApiKeyScheme = "ApiKey";

    public static void Apply(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CookiePolicyOptions>(options => {
            // This lambda determines whether user consent for non-essential cookies is needed for a given request.
            options.CheckConsentNeeded = _ => true;
            options.MinimumSameSitePolicy = SameSiteMode.None;
        });

        // Add authentication services
        var authBuilder = services.AddAuthentication(options => {
            options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        });

        ConfigureAuth0(authBuilder, configuration);
        ConfigureSkillTokens(authBuilder, configuration);
        ConfigureApiTokens(authBuilder);
    }

    static void ConfigureApiTokens(AuthenticationBuilder authBuilder)
    {
        authBuilder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyScheme, null);
    }

    static void ConfigureSkillTokens(AuthenticationBuilder authBuilder, IConfiguration configuration)
    {
        // Get the signing key
        var keyString = configuration["Skill:DataApiKey"]
                         ?? throw new InvalidOperationException($"'Skill:DataApiKey' not set in AppSettings.");
        var securityKey = new SymmetricSecurityKey(Convert.FromBase64String(keyString));

        authBuilder.AddJwtBearer(SkillTokenScheme,
            options => {
                options.TokenValidationParameters.IssuerSigningKey = securityKey;
                options.TokenValidationParameters.ValidIssuer = ApiTokenFactory.TokenIssuer;
                options.TokenValidationParameters.AuthenticationType = SkillTokenScheme;
                options.TokenValidationParameters.AudienceValidator = (audiences, _, _) => {
                    foreach (var aud in audiences)
                    {
                        if (!aud.StartsWith("skillId=", StringComparison.Ordinal))
                        {
                            return false;
                        }

                        if (!int.TryParse(aud["skillId=".Length..], out _))
                        {
                            return false;
                        }
                    }
                    return true;
                };
            });
    }

    static void ConfigureAuth0(AuthenticationBuilder authBuilder, IConfiguration configuration)
    {
        var auth0Domain = configuration[WebConfigurationKeys.Auth0Domain]
                          ?? throw new InvalidOperationException(
                              $"{WebConfigurationKeys.Auth0Domain} not set in AppSettings.");

        var auth0ClientId = configuration[WebConfigurationKeys.Auth0ClientId]
                            ?? throw new InvalidOperationException(
                                $"{WebConfigurationKeys.Auth0ClientId} not set in AppSettings.");

        authBuilder.AddCookie(options => {
            options.Events.OnRedirectToAccessDenied = context => {
                var registrationRequired = context.HttpContext.User.GetRegistrationStatusClaim()
                    is RegistrationStatus.ApprovalRequired;

                if (registrationRequired)
                {
                    context.Response.Redirect(OrganizationStateFilter.RegistrationPage);
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async context => {
                var serviceProvider = context.HttpContext.RequestServices;
                var authenticationHandler = serviceProvider.GetRequiredService<IAuthenticationHandler>();
                await authenticationHandler.HandleValidatePrincipalAsync(context);
            };

            options.LoginPath = "/login";
            options.LogoutPath = "/logout";
            options.AccessDeniedPath = "/status/accessdenied";
        })
            .AddOpenIdConnect("Auth0",
                options => {
                    // Set the authority to your Auth0 domain
                    options.Authority = $"https://{auth0Domain}";

                    // Configure the Auth0 Client ID and Client Secret
                    options.ClientId = auth0ClientId;
                    options.ClientSecret = configuration[WebConfigurationKeys.Auth0ClientSecret];

                    // Set response type to code
                    options.ResponseType = OpenIdConnectResponseType.Code;

                    // Configure the scopes
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.Scope.Add("email");

                    // Set the correct name claim type
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "name",
                        RoleClaimType = ClaimTypes.Role
                    };

                    // Ensure that you have added the URL as an Allowed Callback URL in your Auth0 dashboard
                    options.CallbackPath = new PathString("/callback");

                    // Configure the Claims Issuer to be Auth0
                    options.ClaimsIssuer = "Auth0";

                    options.Events = new OpenIdConnectEvents
                    {
                        OnRemoteFailure = async context => {
                            if (context.Failure?.GetType() == typeof(Exception))
                            {
                                var rae = new RemoteAuthenticationException(context);
                                context.Failure = rae;
                            }
                        },

                        // handle the logout redirection
                        OnRedirectToIdentityProviderForSignOut = context => {
                            var logoutUri = $"https://{auth0Domain}/v2/logout?client_id={auth0ClientId}";

                            var postLogoutUri = context.Properties.RedirectUri;
                            if (!string.IsNullOrEmpty(postLogoutUri))
                            {
                                if (postLogoutUri.StartsWith("/", StringComparison.Ordinal))
                                {
                                    // transform to absolute
                                    var request = context.Request;
                                    postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase +
                                                    postLogoutUri;
                                }

                                logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
                            }

                            context.Response.Redirect(logoutUri);
                            context.HandleResponse();

                            return Task.CompletedTask;
                        },

                        OnTokenValidated = async context => {
                            var serviceProvider = context.HttpContext.RequestServices;
                            var authenticationHandler = serviceProvider.GetRequiredService<IAuthenticationHandler>();
                            await authenticationHandler.HandleAuthenticatedUserAsync(context.Principal.Require());
                        },

                        OnRedirectToIdentityProvider = async context => {
                            // Bypass Auth0's interstitial prompt for a login method.
                            // We only want Slack.
                            context.ProtocolMessage.Parameters.Add("connection", "slack");
                        },
                    };
                });
    }

#pragma warning disable CA1032
    public class RemoteAuthenticationException : Exception
#pragma warning restore CA1032
    {
        public RemoteAuthenticationException(RemoteFailureContext context)
            : base(context.Failure?.Message, context.Failure)
        {
        }
    }
}
