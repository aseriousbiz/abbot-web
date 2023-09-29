using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Options;
using Serious.Abbot;
using Serious.Abbot.Configuration;
using Serious.Abbot.Security;
using Serious.TestHelpers;
using Xunit;

public class ApiTokenFactoryTests
{
    public class TheCreateSkillApiTokenMethod
    {
        [Fact]
        public void CreatesTokenForSkillAndOrgUsingConfiguredSecret()
        {
            long timestamp = 8675309;
            var options = Options.Create(
                new SkillOptions
                {
                    DataApiKey =
                        "IRWb9gsquqVuwh5TQRLA5kX0Ih/T18zMIPt2vmnYi6+u/XK5q0kJwTVLwiuBaqsTLYUknXh1F2p9vXZ2IVUh3jpw"
                });
            var tokenFactory = new ApiTokenFactory(options);
            const int skillId = 4;
            const int userId = 1;
            const int memberId = 42;

            var token = tokenFactory.CreateSkillApiToken(new(skillId), new(memberId), new(userId), timestamp);

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            Assert.NotNull(jwt);
            Assert.Equal("HS256", jwt.Header.Alg);
            Assert.Equal(new[] { "skillId=4" }, jwt.Payload.Aud.ToArray());
            Assert.Equal("userId=1", jwt.Payload.Sub);
            Assert.Equal((int)DateTimeOffset.UnixEpoch.AddSeconds(timestamp).AddHours(1).ToUnixTimeSeconds(), jwt.Payload.Exp);
            Assert.Equal(ApiTokenFactory.TokenIssuer, jwt.Payload.Iss);
        }
    }
}
