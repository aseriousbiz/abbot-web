using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Security;

public class ApiTokenFactory : IApiTokenFactory
{
    public static readonly string AbbotClaimPrefix = "https://schema.ab.bot/claims/";
    public static readonly string MemberIdClaimType = $"{AbbotClaimPrefix}memberId";
    public static readonly string SkillIdClaimType = $"{AbbotClaimPrefix}skillId";

    readonly IOptions<SkillOptions> _options;
    readonly SigningCredentials _signingCreds;
    readonly JwtSecurityTokenHandler _handler;

    public static string TokenIssuer => $"https://{WebConstants.DefaultHost}";

    public static bool IsAbbotClaim(Claim claim) => claim.Type.StartsWith(AbbotClaimPrefix, StringComparison.Ordinal);

    public ApiTokenFactory(IOptions<SkillOptions> options)
    {
        _options = options;
        var key = new SymmetricSecurityKey(Convert.FromBase64String(options.Value.DataApiKey.Require()));
        _signingCreds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        _handler = new JwtSecurityTokenHandler();
    }

    public string CreateSkillApiToken(Id<Skill> skillId, Id<Member> memberId, Id<User> userId, long timestamp)
    {
        var ts = DateTime.UnixEpoch.AddSeconds(timestamp);
        var jwt = new JwtSecurityToken(
            issuer: TokenIssuer,
            audience: $"skillId={skillId.Value}",
            expires: ts.AddHours(1),
            claims: CreateSkillTokenClaims(skillId, memberId, userId),
            signingCredentials: _signingCreds);

        return _handler.WriteToken(jwt);
    }

    public static IReadOnlyList<Claim> CreateSkillTokenClaims(Id<Skill> skillId, Id<Member> memberId, Id<User> userId)
    {
        return new[]
        {
            // For legacy reasons, the subject claim is the user Id
            new Claim("sub", $"userId={userId.Value}"),

            // Load it up with our custom claims.
            // Once these are consistently used, we can change the "sub" claim freely.
            new Claim(MemberIdClaimType, $"{memberId.Value}"),
            new Claim(SkillIdClaimType, $"{skillId.Value}"),
        };
    }
}
