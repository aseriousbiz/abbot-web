using Serious.Abbot.Entities;

namespace Serious.Abbot.Security;

/// <summary>
/// Used to create and validate Skill API Tokens.
/// </summary>
/// <remarks>
/// When calling one of the skill runners, Abbot.Web generates an API Token
/// that it sends in the header. The skill runners have to send that token
/// back. This secures the skill api.
/// </remarks>
public interface IApiTokenFactory
{
    /// <summary>
    /// Creates a skill API token used to call a skill.
    /// </summary>
    /// <param name="skillId">The id of the <see cref="Skill"/></param>
    /// <param name="memberId">The id of the <see cref="Member"/> making the call</param>
    /// <param name="userId">The id of the <see cref="User"/> making the call</param>
    /// <param name="timestamp">The time the token was created</param>
    string CreateSkillApiToken(Id<Skill> skillId, Id<Member> memberId, Id<User> userId, long timestamp);
}
