using System.Collections.Generic;
using System.Security.Claims;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;

namespace Serious.TestHelpers
{
    public class FakeClaimsPrincipal : ClaimsPrincipal
    {
        public FakeClaimsPrincipal(Member member, RegistrationStatus registrationStatus = RegistrationStatus.Ok)
            : this(
                member.Organization.PlatformId,
                member.User.PlatformUserId,
                member.Organization.Name ?? "A Cool Team",
                member.Organization.Domain ?? "test.slack.com",
                member.User.DisplayName,
                member.User.Email ?? "tester@example.com",
                member.User.Avatar ?? "https://example.com/mug",
                registrationStatus)
        {
            foreach (var memberRole in member.MemberRoles)
            {
                this.AddRoleClaim(memberRole.Role.Name);
            }
        }

        public FakeClaimsPrincipal(
            string? platformId = "T001",
            string platformUserId = "U001",
            string platformTeamName = "A Cool Team",
            string domain = "test.slack.com",
            string? name = "A Cool Person",
            string? email = "tester@example.com",
            string avatar = "https://example.com/mug",
            RegistrationStatus registrationStatus = RegistrationStatus.Ok,
            string? nameIdentifier = null,
            string? enterpriseId = null,
            string? enterpriseName = null,
            string? enterpriseDomain = null
        )
            : base(
                new ClaimsIdentity(
                    GetDefaultClaims(
                        platformId,
                        platformUserId,
                        platformTeamName,
                        domain,
                        name,
                        email,
                        avatar,
                        registrationStatus,
                        nameIdentifier,
                        enterpriseId,
                        enterpriseName,
                        enterpriseDomain), "OpenId"))
        {
        }

        static IEnumerable<Claim> GetDefaultClaims(
            string? platformId,
            string platformUserId,
            string platformTeamName,
            string domain,
            string? name,
            string? email,
            string? avatar,
            RegistrationStatus registrationStatus,
            string? nameIdentifier,
            string? enterpriseId,
            string? enterpriseName,
            string? enterpriseDomain)
        {
            if (name is not null)
            {
                yield return new Claim(ClaimTypes.Name, name);
            }
            yield return new Claim(ClaimTypes.NameIdentifier, nameIdentifier ?? $"oauth|slack|{platformUserId}");
            yield return new Claim($"{AbbotSchema.SchemaUri}platform_user_id", platformUserId);
            if (platformId is not null)
            {
                yield return new Claim($"{AbbotSchema.SchemaUri}platform_id", platformId);
            }
            yield return new Claim($"{AbbotSchema.SchemaUri}platform_name", platformTeamName);
            if (avatar is not null)
            {
                yield return new Claim("picture", avatar);
            }
            yield return new Claim($"{AbbotSchema.SchemaUri}platform_domain", domain);
            if (email is not null)
            {
                yield return new Claim(ClaimTypes.Email, email);
            }

            if (registrationStatus != RegistrationStatus.Ok)
            {
                yield return new Claim("RegistrationStatus", registrationStatus.ToString());
            }
            if (enterpriseId is not null)
            {
                yield return new Claim($"{AbbotSchema.SchemaUri}enterprise_id", enterpriseId);
            }
            if (enterpriseName is not null)
            {
                yield return new Claim($"{AbbotSchema.SchemaUri}enterprise_name", enterpriseName);
            }
            if (enterpriseDomain is not null)
            {
                yield return new Claim($"{AbbotSchema.SchemaUri}enterprise_domain", enterpriseDomain);
            }
        }

        public static ClaimsPrincipal ForSkillToken(Id<Skill> skillId, Id<Member> memberId, Id<User> userId)
        {
            var claims = ApiTokenFactory.CreateSkillTokenClaims(skillId, memberId, userId);
            var identity = new ClaimsIdentity(claims, "SkillToken");
            return new ClaimsPrincipal(new[] { identity });
        }
    }
}
