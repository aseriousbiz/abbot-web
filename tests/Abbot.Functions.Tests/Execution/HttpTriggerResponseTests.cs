using Serious.Abbot.Execution;
using Xunit;

public class HttpTriggerResponseTests
{
    public class TheContentProperty
    {
        [Fact]
        public void SetsRawContentToSerializedContent()
        {
            var content = new { foo = "bar", bar = new[] { 1, 2 } };

            var response = new HttpTriggerResponse
            {
                Content = content
            };

            Assert.Equal("{\"foo\":\"bar\",\"bar\":[1,2]}", response.RawContent);
            Assert.Same(content, response.Content);
        }

        [Fact]
        public void OverwritesRawContentToSerializedContent()
        {
            var content = new { foo = "bar", bar = new[] { 1, 2 } };
            var response = new HttpTriggerResponse
            {
                RawContent = "I am a teapot"
            };
            Assert.Equal("I am a teapot", response.RawContent);

            response.Content = content;

            Assert.Equal("{\"foo\":\"bar\",\"bar\":[1,2]}", response.RawContent);
            Assert.Same(content, response.Content);
        }
    }

    public class TheRawContentProperty
    {
        [Fact]
        public void SetsRawContentToValueAndContentToNull()
        {
            var response = new HttpTriggerResponse
            {
                Content = new { foo = "bar", bar = new[] { 1, 2 } }
            };
            Assert.Equal("{\"foo\":\"bar\",\"bar\":[1,2]}", response.RawContent);

            response.RawContent = "{\"success\": true}";

            Assert.Equal("{\"success\": true}", response.RawContent);
            Assert.Null(response.Content);
        }
    }
}
