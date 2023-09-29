using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class UsersClientTests
{

    public class TheGetConversationMethod
    {
        [Fact]
        public void ReturnsUserFromProvidedId()
        {
            var client = new UsersClient(new FakeSkillApiClient(42));
            var user = client.GetTarget("U12345");

            Assert.Equal("U12345", user.Id);
            Assert.Equal(new ChatAddress(ChatAddressType.User, "U12345"), user.Address);
            Assert.Equal(new ChatAddress(ChatAddressType.User, "U12345", "T1"), user.GetThread("T1").Address);
        }
    }
}
