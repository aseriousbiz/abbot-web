using Serious.Abbot.Entities;
using Serious.Abbot.Security;

namespace Serious.TestHelpers
{
    public class FakeApiTokenFactory : IApiTokenFactory
    {
        readonly string _secret;

        public FakeApiTokenFactory() : this("secret")
        {
        }

        FakeApiTokenFactory(string secret)
        {
            _secret = secret;
        }

        public string CreateSkillApiToken(Id<Skill> skillId, Id<Member> memberId, Id<User> userId, long timestamp)
        {
            return _secret;
        }

        public bool ValidateSkillApiToken(string token, int skillId, int userId, long timestamp)
        {
            return token == _secret;
        }
    }
}
