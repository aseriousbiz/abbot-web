using HtmlAgilityPack;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeBotHttpClient : IBotHttpClient
    {
        public Task<dynamic?> SendJsonAsync(Uri url, HttpMethod httpMethod, object? content, Headers headers)
        {
            throw new NotImplementedException();
        }

        public Task<AbbotResponse<TResponse>> SendJsonAsAsync<TResponse>(Uri url, HttpMethod httpMethod, object? content, Headers headers)
        {
            throw new NotImplementedException();
        }

        public Task<HtmlNode?> ScrapeAsync(Uri url, string selector)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<HtmlNode>> ScrapeAllAsync(Uri url, string selector)
        {
            throw new NotImplementedException();
        }
    }
}
