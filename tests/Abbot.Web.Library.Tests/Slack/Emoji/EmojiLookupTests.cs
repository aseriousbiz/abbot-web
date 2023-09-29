using System;
using System.Threading.Tasks;
using Serious;
using Serious.Slack;
using Xunit;

public class EmojiLookupTests
{
    public class TheGetEmojiAsyncMethod
    {
        [Fact]
        public async Task ReturnsUnicodeEmoji()
        {
            var customEmojiLookup = new FakeCustomEmojiLookup();
            var lookup = new EmojiLookup(customEmojiLookup);

            var result = await lookup.GetEmojiAsync("+1", "AccessToken");

            var unicodeEmoji = Assert.IsType<UnicodeEmoji>(result);
            var (name, entity) = unicodeEmoji;
            Assert.Equal(("+1", "👍"), (name, entity));
        }

        [Fact]
        public async Task ReturnsCustomEmoji()
        {
            var customEmojiLookup = new FakeCustomEmojiLookup();
            customEmojiLookup.AddEmoji("AccessToken", new CustomEmoji("abbot", new Uri("https://example.com/abbot.jpg")));
            var lookup = new EmojiLookup(customEmojiLookup);

            var result = await lookup.GetEmojiAsync("abbot", "AccessToken");

            var customEmoji = Assert.IsType<CustomEmoji>(result);
            var (name, url) = customEmoji;
            Assert.Equal(("abbot", new Uri("https://example.com/abbot.jpg")), (name, url));
        }
    }
}
