using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Serious.Abbot.Integrations.GitHub;

public class GitHubJwtFactory
{
    readonly GitHubOptions _gitHubOptions;
    readonly IClock _clock;

    public GitHubJwtFactory(IOptions<GitHubOptions> gitHubOptions, IClock clock)
    {
        _gitHubOptions = gitHubOptions.Value;
        _clock = clock;
    }

    public string GenerateJwt()
    {
        var appId = _gitHubOptions.AppId.Require("Required setting 'GitHub:AppId' is missing");
        var appKey = _gitHubOptions.AppKey.Require("Required setting 'GitHub:AppKey' is missing");

        using var rsaKey = RSA.Create();
        rsaKey.ImportFromPem(appKey);

        var securityKey = new RsaSecurityKey(rsaKey)
        {
            CryptoProviderFactory = { CacheSignatureProviders = false }
        };
        var jwtSigningCreds = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        var header = new JwtHeader(jwtSigningCreds);

        var payload = new JwtPayload(
            appId,
            "https://api.github.com", // Don't think GitHub cares about this field.
            Array.Empty<Claim>(),
            null,
            _clock.UtcNow.AddMinutes(5),
            _clock.UtcNow.AddMinutes(-1)); // Set iat to 1 minute in the past to account for clock skew.

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(new JwtSecurityToken(header, payload));
    }
}
