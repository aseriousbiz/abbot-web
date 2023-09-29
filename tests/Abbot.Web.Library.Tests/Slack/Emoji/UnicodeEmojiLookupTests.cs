using System.Threading.Tasks;
using Serious;
using Xunit;

public class UnicodeEmojiLookupTests
{
    public class TheTryGetEmojiMethod
    {
        [Fact]
        public async Task ReturnsTrueForFoundEmoji()
        {
            var emoji = await UnicodeEmojiLookup.GetEmojiOrDefaultAsync("+1");

            Assert.NotNull(emoji);
            Assert.Equal(("+1", "&#128077;"), (emoji.Name, emoji.HtmlEncoded));
        }

        [Fact]
        public async Task ReturnsFalseForNotFoundEmoji()
        {
            var emoji = await UnicodeEmojiLookup.GetEmojiOrDefaultAsync("abbbotttt");

            Assert.Null(emoji);
        }

        [Theory]
        // NOTE: Some of these emoji _don't need to be encoded_ so WebUtility.HtmlEncode() just returns the original.
        [InlineData("heart", "❤️", "❤️")]
        [InlineData("hash", "#️⃣", "#️⃣")]
        [InlineData("flag-ca", "🇨🇦", "&#127464;&#127462;")]
        [InlineData("laughing", "😆", "&#128518;")]
        public async Task ReturnsExpectedStringsForEmoji(string emojiName, string str, string htmlEncoded)
        {
            var emoji = await UnicodeEmojiLookup.GetEmojiOrDefaultAsync(emojiName);
            Assert.NotNull(emoji);
            Assert.Equal(str, emoji.Emoji);
            Assert.Equal(htmlEncoded, emoji.HtmlEncoded);
        }
    }
}
