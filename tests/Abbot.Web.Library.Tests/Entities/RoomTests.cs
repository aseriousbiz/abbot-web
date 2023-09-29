
using Serious.Abbot.Entities;

public class RoomTests
{
    public class TheToMentionMethod
    {
        [Theory]
        [InlineData(RoomType.PublicChannel, true, "<#C012ZJGPYTF>")]
        [InlineData(RoomType.PublicChannel, false, "<#C012ZJGPYTF>")]
        [InlineData(RoomType.PrivateChannel, false, "<#C012ZJGPYTF>")]
        [InlineData(RoomType.PrivateChannel, true, "<https://unit-test.slack.com/archives/C012ZJGPYTF|#general>")]
        [InlineData(RoomType.DirectMessage, true, "<https://unit-test.slack.com/archives/C012ZJGPYTF|#general>")]
        [InlineData(RoomType.MultiPartyDirectMessage, true, "<https://unit-test.slack.com/archives/C012ZJGPYTF|#general>")]
        [InlineData(RoomType.Unknown, true, "<https://unit-test.slack.com/archives/C012ZJGPYTF|#general>")]
        public void ReturnsLinkOrMention(RoomType roomType, bool useLinkForPrivateRoom, string expected)
        {
            var room = new Room
            {
                Organization = new Organization
                {
                    Domain = "unit-test.slack.com"
                },
                Name = "general",
                RoomType = roomType,
                PlatformRoomId = "C012ZJGPYTF",
            };

            var result = room.ToMention(useLinkForPrivateRoom);

            Assert.Equal(expected, result);
        }
    }
}
