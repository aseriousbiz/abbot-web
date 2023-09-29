using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Serious.Abbot;
using Serious.Abbot.Functions.Clients;
using Serious.Abbot.Messages;
using Serious.TestHelpers;
using Xunit;

public class BrainApiClientTests
{
    public class TheGetSkillDataAsyncMethod
    {
        [Fact]
        public async Task RetrievesSkillDataItem()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/brain?key=the-key%2Fcan-be%2Fanything");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, new SkillDataResponse { Value = "stored value" });
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            var result = await brainApiClient.GetSkillDataAsync("the-key/can-be/anything");

            Assert.NotNull(result);
            Assert.Equal("stored value", result.Value);
        }

        [Fact]
        public async Task ReturnsNullForMissingItem()
        {
            var apiClient = new FakeSkillApiClient(42);
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            var result = await brainApiClient.GetSkillDataAsync("wrong-key");

            Assert.Null(result);
        }

        [Theory]
        [InlineData("the-key/can-be/anything", SkillDataScope.Organization, null)]
        [InlineData("the-key/can-be/anything", SkillDataScope.Conversation, "1")]
        public async Task RetrievesSkillDataItemWithScopeAndContextParameters(string key, SkillDataScope scope, string? contextId)
        {
            var args = new Dictionary<string, string?>
            {
                {"key", key},
                {"scope", scope.ToString()},
            };

            if (contextId != null)
                args.Add("contextId", contextId);

            var expectedUrl = new Uri(QueryHelpers.AddQueryString("https://ab.bot/api/skills/42/brain", args));

            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, new SkillDataResponse { Key = key, Value = "stored value" });
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            var result = await brainApiClient.GetSkillDataAsync(key, scope, contextId);

            Assert.NotNull(result);
            Assert.Equal("stored value", result.Value);
        }

        [Theory]
        [InlineData("the-key/can-be/anything", SkillDataScope.Conversation, null)]
        [InlineData("the-key/can-be/anything", SkillDataScope.Room, null)]
        [InlineData("the-key/can-be/anything", SkillDataScope.User, null)]
        public async Task ThrowsForMissingScopeAndContextParameters(string key, SkillDataScope scope, string contextId)
        {
            var apiClient = new FakeSkillApiClient(42);
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            await Assert.ThrowsAsync<ArgumentNullException>(() => brainApiClient.GetSkillDataAsync(key, scope, contextId));
        }
    }

    public class TheGetAllDataAsyncMethod
    {
        [Fact]
        public async Task RetrievesAllSkillData()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/brain");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, new Dictionary<string, string>
            {
                {"key1", "value1"},
                {"key2", "value2"}
            });
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            var result = await brainApiClient.GetAllDataAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("value1", result["key1"]);
            Assert.Equal("value2", result["key2"]);
        }
    }

    public class TheGetSkillDataKeysAsyncMethod
    {
        [Fact]
        public async Task RetrievesAllSkillDataKeys()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/brain");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, new Dictionary<string, string>
            {
                {"key1", "value1"},
                {"key2", "value2"}
            });
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            var result = await brainApiClient.GetSkillDataKeysAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("key1", result[0]);
            Assert.Equal("key2", result[1]);
        }
    }

    public class ThePostDataAsyncMethod
    {
        [Fact]
        public async Task PostsNewSkillData()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/brain?key=some-key");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Post, new SkillDataResponse
            {
                Key = "some-key",
                Value = "some-value"
            });
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            var result = await brainApiClient.PostDataAsync("some-key", "some-value");

            Assert.NotNull(result);
            Assert.Equal("some-key", result.Key);
            Assert.Equal("some-value", result.Value);
        }
    }

    public class TheDeleteDataAsyncMethod
    {
        [Fact]
        public async Task DeletesSkillData()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/brain?key=some-key");
            var apiClient = new FakeSkillApiClient(42);
            bool deleteCalled = false;
            Func<HttpResponseMessage> callback = () => {
                deleteCalled = true;
                return new HttpResponseMessage();
            };
            apiClient.AddResponse(expectedUrl, HttpMethod.Delete, callback);
            var brainApiClient = new BrainApiClient(apiClient, new FakeSkillContextAccessor(42));

            await brainApiClient.DeleteDataAsync("some-key");
            Assert.True(deleteCalled);
        }
    }
}
