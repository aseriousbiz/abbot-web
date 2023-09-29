using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Refit;
using Serious;
using Serious.Abbot.Clients;
using Serious.Slack;
using Xunit;

public class CachingCustomEmojiLookupTests
{
    public class TheGetAllAsyncMethod
    {
        [Fact]
        public async Task CallsApiAndCachesResponse()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IEmojiClient>(out var emojiClient)
                .Build();
            var emojiResponse = new EmojiListResponse
            {
                Ok = true,
                Body = new Dictionary<string, string>
                {
                    { "smile", "http://example.com/smile.png" },
                    { "frown", "http://example.com/frown.png" }
                }
            };
            emojiClient.GetCustomEmojiListAsync("apitoken")
                .Returns(emojiResponse, new EmojiListResponse { Ok = false });
            var lookup = env.Activate<CachingCustomEmojiLookup>();
            var result = await lookup.GetAllAsync(new Dictionary<string, UnicodeEmoji>(), "apitoken");

            var initialResult = result.Single(r => r.Name == "smile");
            var cachedResult = await lookup.GetEmojiAsync("frown", "apitoken");

            var customEmoji = Assert.IsType<CustomEmoji>(initialResult);
            Assert.Equal(new Uri("http://example.com/smile.png"), customEmoji.ImageUrl);
            var customCachedEmoji = Assert.IsType<CustomEmoji>(cachedResult);
            Assert.Equal(new Uri("http://example.com/frown.png"), customCachedEmoji.ImageUrl);
            await emojiClient.Received(1).GetCustomEmojiListAsync("apitoken");
        }

        [Fact]
        public async Task ReturnsNullIfApiExceptionAndCachesNullResponseForAMoment()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .Substitute<IEmojiClient>(out var emojiClient)
                .Build();
            var apiException = await env.CreateApiExceptionAsync(
                HttpStatusCode.Forbidden,
                HttpMethod.Get,
                uri: "https://example.com",
                payload: new {
                });
            var emojiResponse = new EmojiListResponse
            {
                Ok = true,
                Body = new Dictionary<string, string>
                {
                    { "smile", "http://example.com/smile.png" },
                    { "frown", "http://example.com/frown.png" }
                }
            };
            emojiClient.GetCustomEmojiListAsync("apitoken")
                .Returns(_ => throw apiException, _ => emojiResponse);
            var lookup = env.Activate<CachingCustomEmojiLookup>();

            var result = await lookup.GetAllAsync(new Dictionary<string, UnicodeEmoji>(), "apitoken");
            var cachedResult = await lookup.GetAllAsync(new Dictionary<string, UnicodeEmoji>(), "apitoken");

            Assert.Empty(result);
            Assert.Empty(cachedResult);
        }
    }
}
