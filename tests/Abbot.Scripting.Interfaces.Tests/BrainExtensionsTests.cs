using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Storage;
using Serious.TestHelpers;

public class BrainExtensionsTests
{
    public class TheGetListAsyncMethod
    {
        [Fact]
        public async Task ReturnsStoredList()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToListAsync("key", "test");

            var list = await botBrain.GetListAsync<string>("key");

            Assert.Equal("test", list.Single());
        }
    }

    public class TheAddToListAsyncMethod
    {
        [Fact]
        public async Task CreatesNewList()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);

            var list = await botBrain.AddToListAsync("key", "test");
            Assert.Equal("test", list.Single());
            var stored = await botBrain.GetAsAsync<List<string>>("key");
            Assert.NotNull(stored);
            Assert.Equal("test", stored!.Single());
        }

        [Fact]
        public async Task AddsToExistingList()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);

            await botBrain.AddToListAsync("key", "test1");
            var list = await botBrain.AddToListAsync("key", "test2");

            Assert.Equal(2, list.Count);
            Assert.Equal("test1", list.First());
            Assert.Equal("test2", list.Last());
            var stored = await botBrain.GetAsAsync<List<string>>("key");
            Assert.NotNull(stored);
            Assert.Equal(2, stored!.Count);
            Assert.Equal("test1", stored.First());
            Assert.Equal("test2", stored.Last());
        }
    }

    public class TheGetHashSetAsyncMethod
    {
        [Fact]
        public async Task ReturnsStoredHashSet()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToHashSetAsync("key", "test");

            var list = await botBrain.GetHashSetAsync<string>("key");

            Assert.Equal("test", list.Single());
        }
    }

    public class TheAddToHashSetAsyncMethod
    {
        [Fact]
        public async Task CreatesNewHashSet()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);

            var added = await botBrain.AddToHashSetAsync("key", "test");
            Assert.True(added);
            var stored = await botBrain.GetAsAsync<HashSet<string>>("key");
            Assert.NotNull(stored);
            Assert.Equal("test", stored!.Single());
        }

        [Fact]
        public async Task AddsToExistingList()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            Assert.True(await botBrain.AddToHashSetAsync("key", "test1"));
            Assert.True(await botBrain.AddToHashSetAsync("key", "test2"));

            var stored = await botBrain.GetAsAsync<HashSet<string>>("key");
            Assert.NotNull(stored);
            Assert.Equal(2, stored!.Count);
            Assert.Equal("test1", stored.First());
            Assert.Equal("test2", stored.Last());
        }

        [Fact]
        public async Task DoesNotAddIfAlreadyExists()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);

            await botBrain.AddToHashSetAsync("key", "test1");
            var added = await botBrain.AddToHashSetAsync("key", "test1");

            Assert.False(added);
            var stored = await botBrain.GetAsAsync<HashSet<string>>("key");
            Assert.NotNull(stored);
            var storedItem = Assert.Single(stored);
            Assert.Equal("test1", storedItem);
        }
    }

    public class TheRemoveFromListMethod
    {
        [Fact]
        public async Task RemovesFromExistingList()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToListAsync("key", "test1");
            await botBrain.AddToListAsync("key", "test2");

            var removed = await botBrain.RemoveFromListAsync("key", "test2");

            Assert.True(removed);
            var list = await botBrain.GetListAsync<string>("key");

            Assert.NotNull(list);
            Assert.Equal("test1", Assert.Single(list));
        }

        [Fact]
        public async Task ReturnsFalseIfItemDoesNotExist()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToListAsync("key", "test1");
            await botBrain.AddToListAsync("key", "test2");

            var removed = await botBrain.RemoveFromListAsync("unknown", "test2");

            Assert.False(removed);
            var list = await botBrain.GetListAsync<string>("key");
            Assert.NotNull(list);
            Assert.Equal(2, list.Count);
        }
    }

    public class TheRemoveAtFromListMethod
    {
        [Fact]
        public async Task RemovesAtFromExistingList()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToListAsync("key", "test1");
            await botBrain.AddToListAsync("key", "test2");

            var removed = await botBrain.RemoveAtFromListAsync<string>("key", 0);

            Assert.True(removed);
            var list = await botBrain.GetListAsync<string>("key");

            Assert.NotNull(list);
            Assert.Equal("test2", Assert.Single(list));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        public async Task ReturnsFalseIfIndexOutOfRangeDoesNotExist(int index)
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToListAsync("key", "test1");
            await botBrain.AddToListAsync("key", "test2");

            var removed = await botBrain.RemoveAtFromListAsync<string>("unknown", index);

            Assert.False(removed);
            var list = await botBrain.GetListAsync<string>("key");
            Assert.NotNull(list);
            Assert.Equal(2, list.Count);
        }
    }

    public class TheRemoveFromHashSetMethod
    {
        [Fact]
        public async Task RemovesFromExistingHashSet()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToHashSetAsync("key", "test1");
            await botBrain.AddToHashSetAsync("key", "test2");

            var removed = await botBrain.RemoveFromHashSetAsync("key", "test2");

            Assert.True(removed);
            var list = await botBrain.GetHashSetAsync<string>("key");
            Assert.NotNull(list);
            Assert.Equal("test1", Assert.Single(list));
        }

        [Fact]
        public async Task ReturnsFalseIfItemDoesNotExist()
        {
            var apiClient = new FakeBrainApiClient();
            var serializer = new BrainSerializer(new FakeSkillContextAccessor());
            var botBrain = new BotBrain(apiClient, serializer);
            await botBrain.AddToHashSetAsync("key", "test1");
            await botBrain.AddToHashSetAsync("key", "test2");

            var removed = await botBrain.RemoveFromHashSetAsync("unknown", "test2");

            Assert.False(removed);
            var hashSet = await botBrain.GetHashSetAsync<string>("key");
            Assert.NotNull(hashSet);
            Assert.Equal(2, hashSet.Count);
        }
    }
}
