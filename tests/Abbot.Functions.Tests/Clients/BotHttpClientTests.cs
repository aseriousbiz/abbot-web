using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Services;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class BotHttpClientTests
{
    public class TheGetJsonMethod
    {
        [Fact]
        public async Task DeserializesJsonAsDynamic()
        {
            var httpMessageHandler = new FakeHttpMessageHandler();
            httpMessageHandler.AddResponse(
                new Uri("https://example.com"),
                HttpMethod.Get,
                new { test = "success!" });
            var httpClient = new HttpClient(httpMessageHandler);
            var botClient = new BotHttpClient(httpClient);

            dynamic? response = await botClient.GetJsonAsync("https://example.com");

            Assert.NotNull(response);
            Assert.Equal("success!", (string)response!.test);
        }
    }

    public class TheDeleteJsonMethod
    {
        [Fact]
        public async Task DeserializesJsonAsDynamic()
        {
            var httpMessageHandler = new FakeHttpMessageHandler();
            httpMessageHandler.AddResponse(
                new Uri("https://example.com"),
                HttpMethod.Delete,
                new { test = "success!" });
            var httpClient = new HttpClient(httpMessageHandler);
            var botClient = new BotHttpClient(httpClient);

            dynamic? response = await botClient.DeleteJsonAsync("https://example.com");

            Assert.NotNull(response);
            Assert.Equal("success!", (string)response!.test);
        }
    }

    public class ThePostJsonAsyncMethod
    {
        [Fact]
        public async Task DeserializesJsonAsDynamic()
        {
            var url = new Uri("https://example.com/");
            var httpMessageHandler = new FakeHttpMessageHandler();
            var requestHandler = httpMessageHandler.AddResponse(
                url,
                HttpMethod.Post,
                new { test = "success!" });
            var httpClient = new HttpClient(httpMessageHandler);
            var botClient = new BotHttpClient(httpClient);

            dynamic? response = await botClient.PostJsonAsync(url.ToString(), new { incoming = "payload" });

            Assert.NotNull(response);
            Assert.Equal("success!", (string)response!.test);
            var request = requestHandler.ReceivedRequest;
            Assert.Equal(HttpMethod.Post, request.Method);
        }
    }

    public class ThePutJsonMethod
    {
        [Fact]
        public async Task DeserializesJsonAsDynamic()
        {
            var url = new Uri("https://example.com/");
            var httpMessageHandler = new FakeHttpMessageHandler();
            var requestHandler = httpMessageHandler.AddResponse(
                url,
                HttpMethod.Put,
                new { test = "success!" });
            var httpClient = new HttpClient(httpMessageHandler);
            var botClient = new BotHttpClient(httpClient);

            dynamic? response = await botClient.PutJsonAsync(url.ToString(), new { incoming = "payload" });

            Assert.NotNull(response);
            Assert.Equal("success!", (string)response!.test);
            var request = requestHandler.ReceivedRequest;
            Assert.Equal(HttpMethod.Put, request.Method);
        }
    }
}
