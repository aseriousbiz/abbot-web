using System.Text.Json;
using Serious.Json;
using Xunit;

public class JsonHelpersTests
{
    public class TheDeserializeJsonToDictionaryMethod
    {
        [Fact]
        public void DeserializesJsonToDictionary()
        {
            const string json = """
{
  "channel": {"id": "C01234", "name": "general"},
  "customer": {"id": 1234, "segments": ["one", "two"] },
  "enabled": true
}
""";

            var result = JsonHelpers.DeserializeJsonToDictionary(json);

            Assert.Equal(3, result.Count);
            var channel = Assert.IsType<Dictionary<string, object?>>(result["channel"]);
            Assert.Equal("C01234", channel["id"]);
            Assert.Equal("general", channel["name"]);
            var customer = Assert.IsType<Dictionary<string, object?>>(result["customer"]);
            Assert.Equal(1234, (long)customer["id"]!);
            var segments = Assert.IsType<object[]>(customer["segments"]);
            Assert.Equal(new[] { "one", "two" }, segments.Cast<string>().ToArray());
            Assert.True((bool)result["enabled"]!);
        }

        [Fact]
        public void DeserializesArrayToEmptyDictionary()
        {
            const string json = """
["one", "two"]
""";

            var result = JsonHelpers.DeserializeJsonToDictionary(json);

            Assert.Empty(result);
        }

        [Fact]
        public void ThrowsForMalformedJson()
        {
            const string json = "{";

            try
            {
                JsonHelpers.DeserializeJsonToDictionary(json);
            }
            catch (JsonException) // Actual exception type thrown is JsonReaderException which is internal.
            {
                return;
            }
            Assert.Fail("Does not throw JsonException");
        }
    }
}
