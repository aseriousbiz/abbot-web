using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Messages;
using Serious.Abbot.Serialization;
using Serious.TestHelpers;
using Xunit;

public class HttpTriggerRequestTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesTriggerFromRequest()
        {
            var request = await FakeHttpRequestFactory.CreateAsync(new { foo = "bar" });
            request.ContentType = "application/json";

            var triggerEvent = await HttpTriggerRequest.CreateAsync(request);

            Assert.Equal("{\"foo\":\"bar\"}", triggerEvent.RawBody);
            Assert.Equal("application/json", triggerEvent.ContentType);
        }
    }

    public class TheType
    {
        [Fact]
        public void CanBeRoundTrippedSerializedViaJson()
        {
            var triggerEvent = new HttpTriggerRequest
            {
                RawBody = "This is the body of the request",
                HttpMethod = "POST",
                Headers = new Dictionary<string, string[]>
                {
                    {"Content-Type", new[] {"application/json"}},
                    {"Authorization", new[] {"Bearer Token"}}
                },
                Query = new Dictionary<string, string[]>
                {
                    {"p", new[]{"1"}},
                    {"filter", new[]{"test"}}
                }
            };
            var json = AbbotJsonFormat.Default.Serialize(triggerEvent);

            var message = AbbotJsonFormat.Default.Deserialize<HttpTriggerRequest>(json);

            Assert.NotNull(message);
            Assert.Equal("POST", message.HttpMethod);
            Assert.Equal("This is the body of the request", message.RawBody);
            Assert.Equal("application/json", triggerEvent.Headers["Content-Type"][0]);
            Assert.Equal("Bearer Token", triggerEvent.Headers["Authorization"][0]);
            Assert.Equal("test", triggerEvent.Query["filter"][0]);
            Assert.Equal("1", triggerEvent.Query["p"][0]);
        }

        [Fact]
        public void CanRoundTripFormValues()
        {
            var triggerEvent = new HttpTriggerRequest
            {
                HttpMethod = "POST",
                ContentType = "application/x-www-form-urlencoded",
                Form = new Dictionary<string, string[]>
                {
                    {"name", new[]{"The wind"}}
                }
            };
            var json = AbbotJsonFormat.Default.Serialize(triggerEvent);

            var message = AbbotJsonFormat.Default.Deserialize<HttpTriggerRequest>(json);

            Assert.NotNull(message);
            Assert.Equal("The wind", message.Form["name"][0]);
        }
    }
}
