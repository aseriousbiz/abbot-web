using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Storage;
using Serious.TestHelpers;
using Xunit;

public class BotBrainTests
{
    public class TheGetAsAsyncMethod
    {
        [Fact]
        public async Task CanReadStoredObject()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            var stack = new Stack<string>();
            stack.Push("test");
            await botBrain.WriteAsync("kid", stack);

            var result = await botBrain.GetAsAsync<Stack<string>>("kid");

            Assert.NotNull(result);
            Assert.Equal("test", result.Pop());
        }

        [Fact]
        public async Task ReturnsNullForMissingObject()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));

            var result = await botBrain.GetAsAsync<Stack<string>>("missing");

            Assert.Null(result);
        }

        [Fact]
        public async Task ReturnsDefaultForMissingStruct()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));

            var result = await botBrain.GetAsAsync<int>("missing");

            Assert.Equal(default, result);
        }

        [Fact]
        public async Task ReturnsSpecifiedDefaultForMissingStruct()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));

            var result = await botBrain.GetAsAsync("missing", 23);

            Assert.Equal(23, result);
        }
    }

    public class TheGetAsyncMethod
    {
        [Fact]
        public async Task ReturnsNullWhenKeyNotFound()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));

            var result = await botBrain.GetAsync("non-existent");

            Assert.Null(result);
        }

        [Fact]
        public async Task CanReadStoredString()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("some-key", "some-value");

            var result = await botBrain.GetAsync("SOME-KEY");

            Assert.NotNull(result);
            Assert.Equal("some-value", (string)result!);
        }

        [Fact]
        public async Task CanReadStoredObjectDynamically()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("kid", new { Kid = "Mia" });

            var result = await botBrain.GetAsync("kid");

            Assert.NotNull(result);
            Assert.Equal("Mia", result!.Kid as string);
        }

        [Fact]
        public async Task CanReadStoredObjectAsType()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            var stack = new Stack<string>();
            stack.Push("test");
            await botBrain.WriteAsync("kid", stack);

            var result = await botBrain.GetAsync("kid") as Stack<string>;

            Assert.NotNull(result);
            Assert.Equal("test", result.Pop());
        }

        [Fact]
        public async Task CanReadStoredArrayOfStrings()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("kid", new List<string> { "test1", "test2" });

            var result = await botBrain.GetAsync("kid");

            Assert.NotNull(result);
            Assert.Equal("test1", result![0] as string);
        }

        [Fact]
        public async Task CanReadStoredArrayOfObjects()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("kid", new List<object> { new { }, new { Field1 = "Test", Field2 = "Something" } });

            var result = await botBrain.GetAsync("kid");

            Assert.NotNull(result);
            var x = result![1]!;
            var v = x.Field1;
            Assert.Equal((object)"Test", v);
        }
    }

    public class TheWriteAsyncMethod
    {
        [Fact]
        public async Task OverwritesPreviousValueWithCaseInsensitiveKey()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("key", "value");
            await botBrain.WriteAsync("KEY", "ANOTHER VALUE");

            var result = await botBrain.GetAsync("key");

            Assert.Equal("ANOTHER VALUE", (string)result!);
        }
    }

    public class TheGetKeysAsyncMethod
    {
        [Fact]
        public async Task CanReadAllKeysForStoredValues()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("some-key", "some-value");
            await botBrain.WriteAsync("another-key", "some-value");

            var result = await botBrain.GetKeysAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("SOME-KEY", result[0]);
            Assert.Equal("ANOTHER-KEY", result[1]);
        }

        [Fact]
        public async Task AppliesFuzzyFilterToKeys()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("key1", "some-value");
            await botBrain.WriteAsync("key2", "some-value");
            await botBrain.WriteAsync("something-else", "some-value");

            var result = await botBrain.GetKeysAsync("key");

            Assert.Equal(2, result.Count);
            Assert.Equal("KEY1", result[0]);
            Assert.Equal("KEY2", result[1]);
        }
    }

    public class TheGetAllAsyncMethod
    {
        [Fact]
        public async Task CanReadAllKeysAndValuesForStoredValues()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("some-key", "some-value");
            await botBrain.WriteAsync("another-key", "another-value");

            var result = await botBrain.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("SOME-KEY", result[0].Key);
            Assert.Equal("some-value", result[0].Value);
            Assert.Equal("ANOTHER-KEY", result[1].Key);
            Assert.Equal("another-value", result[1].Value);
        }
    }

    public class TheDeleteMethod
    {
        [Fact]
        public async Task DeletesValueAndKey()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));
            await botBrain.WriteAsync("some-key", "some-value");

            await botBrain.DeleteAsync("Some-Key");

            Assert.Null(await botBrain.GetAsync("some-key"));
        }

        [Fact]
        public async Task DoesNotThrowIfKeyDoesNotExist()
        {
            var apiClient = new FakeBrainApiClient();
            var botBrain = new BotBrain(apiClient, new BrainSerializer(new FakeSkillContextAccessor()));

            await botBrain.DeleteAsync("some-key");

            Assert.Null(await botBrain.GetAsync("some-key"));
        }
    }
}
