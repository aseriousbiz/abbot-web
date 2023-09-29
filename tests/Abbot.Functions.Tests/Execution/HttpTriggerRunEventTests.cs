using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Serious.Abbot.Functions;
using Serious.Abbot.Messages;
using Xunit;

public class HttpTriggerRunEventTests
{
    public class TheConstructor
    {
        [Fact]
        public void PopulatesPropertiesFromSkillTriggerEvent()
        {
            var skillTriggerEvent = new HttpTriggerRequest
            {
                Url = "https://app.ab.bot/some-trigger",
                HttpMethod = "POST",
                RawBody = "Hello world!",
                ContentType = "text/text",
                Headers = new Dictionary<string, string[]>
                {
                    {"Authorization", new[]{"Bearer Token"}}
                },
                Query = new Dictionary<string, string[]>
                {
                    {"id", new[] {"123"}}
                }
            };

            var triggerEvent = new HttpTriggerEvent(skillTriggerEvent);

            Assert.Equal(HttpMethod.Post, triggerEvent.HttpMethod);
            Assert.Equal("Hello world!", triggerEvent.RawBody);
            Assert.Equal("text/text", triggerEvent.ContentType);
            Assert.Equal("Bearer Token", triggerEvent.Headers["Authorization"]);
            Assert.Equal("123", triggerEvent.Query["id"]);
        }
    }

    public class TheFormProperty
    {
        [Fact]
        public void RetrievesFormCollection()
        {
            var skillTriggerEvent = new HttpTriggerRequest
            {
                Url = "https://app.ab.bot/some-trigger",
                HttpMethod = "POST",
                ContentType = "application/x-www-form-urlencoded",
                IsForm = true,
                Form = new Dictionary<string, string[]> { { "type", new[] { "submit" } } }
            };
            var triggerEvent = new HttpTriggerEvent(skillTriggerEvent);

            var result = triggerEvent.Form["type"];

            Assert.Equal("submit", result);
        }

        [Fact]
        public void RetrievesFormCollectionMultipleValues()
        {
            var skillTriggerEvent = new HttpTriggerRequest
            {
                Url = "https://app.ab.bot/some-trigger",
                HttpMethod = "POST",
                ContentType = "application/x-www-form-urlencoded",
                IsForm = true,
                Form = new Dictionary<string, string[]> { { "id", new[] { "1", "2" } } }
            };
            var triggerEvent = new HttpTriggerEvent(skillTriggerEvent);

            var result = triggerEvent.Form["id"];

            Assert.Equal("1,2", result);
            Assert.Equal("1", result[0]);
            Assert.Equal("2", result[1]);
        }
    }

    public class TheBodyProperty
    {
        [Fact]
        public void IsDeserializedFromRawBody()
        {
            var skillTriggerEvent = new HttpTriggerRequest
            {
                Url = "https://app.ab.bot/some-trigger",
                HttpMethod = "POST",
                RawBody = @"{""result"": {""status"": ""ok""}}",
                ContentType = "application/json",
                IsJson = true
            };
            var triggerEvent = new HttpTriggerEvent(skillTriggerEvent);

            var result = (string)triggerEvent.Body.result.status;

            Assert.Equal("ok", result);
        }
    }

    public class TheDeserializeBodyAsMethod
    {
        [Fact]
        public void DeserializesToType()
        {
            var skillTriggerEvent = new HttpTriggerRequest
            {
                Url = "https://app.ab.bot/some-trigger",
                HttpMethod = "POST",
                RawBody = @"{""result"": {""status"": ""ok""}}",
                ContentType = "application/json",
                IsJson = true
            };
            var triggerEvent = new HttpTriggerEvent(skillTriggerEvent);

            var result = triggerEvent.DeserializeBodyAs<Response>()?.Result.Status;

            Assert.Equal("ok", result);
        }

        public class Response
        {
            [JsonProperty("result")]
            public Result Result { get; set; } = null!;
        }

        public class Result
        {
            [JsonProperty("status")]
            public string Status { get; set; } = null!;
        }
    }
}
