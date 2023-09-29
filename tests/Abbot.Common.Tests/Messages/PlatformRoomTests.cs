using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Xunit;

public class PlatformRoomTests
{
    [Fact]
    public void AddressIsDerivedFromId()
    {
        var room = new PlatformRoom("C42", "midgar");
        Assert.Equal(new(ChatAddressType.Room, "C42"), room.Address);
    }

    [Fact]
    public void GetThreadReturnsAddressForSpecificThread()
    {
        var room = new PlatformRoom("C42", "midgar");
        var thread = ((IRoom)room).GetThread("123");
        Assert.Equal(new(ChatAddressType.Room, "C42", "123"), thread.Address);
    }

    [Theory]
    [InlineData("room-id", "room-name", "`#room-name` (`room-id`)")]
    [InlineData("room-id", "", "_a channel with an unknown name_ (`room-id`)")]
    public void ToAuditLogString_FormatsRoomForAuditLog(string id, string name, string expected)
    {
        var chatRoom = new PlatformRoom(id, name);

        var result = chatRoom.ToAuditLogString();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToString_FormatsForRoomLink()
    {
        var room = new PlatformRoom("C0001", "the-room");

        var result = room.ToString();

        Assert.Equal("<#C0001>", result);
    }
}
