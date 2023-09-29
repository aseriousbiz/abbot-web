using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Xunit;

public class PlatformUserTests
{
    public class TheToStringMethod
    {
        [Fact]
        public void ReturnsSlackFormattedUserId()
        {
            var user = new PlatformUser
            {
                Id = "U001",
                Name = "Jason",
                UserName = "costello"
            };

            var result = user.ToString();

            Assert.Equal("<@U001>", result);
        }
    }

    [Fact]
    public void AddressIsDerivedFromId()
    {
        var user = new PlatformUser
        {
            Id = "U001",
            Name = "Jason",
            UserName = "Costello",
            Location = new Location(new Coordinate(123, 124), "98008", "America/Los_Angeles")
        };

        Assert.Equal(new(ChatAddressType.User, "U001"), user.Address);
    }

    [Fact]
    public void GetThreadReturnsAddressForSpecificThread()
    {
        var user = new PlatformUser
        {
            Id = "U001",
            Name = "Jason",
            UserName = "Costello",
            Location = new Location(new Coordinate(123, 124), "98008", "America/Los_Angeles")
        };

        var thread = ((IChatUser)user).GetThread("123");
        Assert.Equal(new(ChatAddressType.User, "U001", "123"), thread.Address);
    }
}
