using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serious;
using Serious.Slack;
using Xunit;

public class EmojiListResponseExtensionsTests
{
    public class TheResolveCustomEmojiAsyncMethod
    {
        [Fact]
        public async Task ReturnsFalseIfResponseNotOk()
        {
            var response = new EmojiListResponse
            {
                Ok = false
            };

            var emoji = await response.ResolveCustomEmojiAsync("emoji");

            Assert.Null(emoji);
        }

        [Fact]
        public async Task ReturnsFalseIfEmojiDoesNotExist()
        {
            var response = new EmojiListResponse
            {
                Ok = true,
                Body = new Dictionary<string, string> { { "not-emoji", "url" } }
            };

            var emoji = await response.ResolveCustomEmojiAsync("emoji");

            Assert.Null(emoji);
        }

        [Fact]
        public async Task ReturnsTrueAndUrlIfEmojiExists()
        {
            var response = new EmojiListResponse
            {
                Ok = true,
                Body = new Dictionary<string, string> { { "emoji", "https://example.com/emoji.gif" } }
            };

            var emoji = await response.ResolveCustomEmojiAsync("emoji");

            Assert.NotNull(emoji);
            var (name, url) = Assert.IsType<CustomEmoji>(emoji);
            Assert.Equal(("emoji", new Uri("https://example.com/emoji.gif")), (name, url));
        }

        [Fact]
        public async Task ReturnsTrueAndReturnsCustomEmojiIfEmojiIsAliasToExistingCustomEmoji()
        {
            var response = new EmojiListResponse
            {
                Ok = true,
                Body = new Dictionary<string, string>
                {
                    { "emoji", "https://example.com/emoji.gif" },
                    { "fun", "alias:emoji" }
                }
            };

            var emoji = await response.ResolveCustomEmojiAsync("fun");

            Assert.NotNull(emoji);
            var (name, url) = Assert.IsType<CustomEmoji>(emoji);
            Assert.Equal(("emoji", new Uri("https://example.com/emoji.gif")), (name, url));
        }

        [Fact]
        public async Task ReturnsTrueAndReturnsUnicodeEmojiIfEmojiIsAliasToUnicodeEmoji()
        {
            var response = new EmojiListResponse
            {
                Ok = true,
                Body = new Dictionary<string, string>
                {
                    { "emoji", "alias:+1" }
                }
            };

            var emoji = await response.ResolveCustomEmojiAsync("emoji");

            Assert.NotNull(emoji);
            var (name, entity) = Assert.IsType<UnicodeEmoji>(emoji);
            Assert.Equal(("+1", "👍"), (name, entity));
        }

        [Fact]
        public async Task ReturnsFalseIfAliasToExistingEmojiNotFound()
        {
            var response = new EmojiListResponse
            {
                Ok = true,
                Body = new Dictionary<string, string>
                {
                    { "emoji", "https://example.com/emoji.gif" },
                    { "fun", "alias:not-emoji" }
                }
            };

            var emoji = await response.ResolveCustomEmojiAsync("fun");

            Assert.Null(emoji);
        }
    }
}
