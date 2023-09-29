using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Options;

namespace Serious.TestHelpers
{
    public class FakeHttpClientFactory : IHttpClientFactory
    {
        readonly Dictionary<string, HttpClient> _httpClients = new();

        public FakeHttpClientFactory()
        {
        }

        public FakeHttpClientFactory(HttpClient httpClient)
        {
            AddHttpClient(Options.DefaultName, httpClient);
        }

        public void AddHttpClient(string name, HttpClient httpClient)
        {
            _httpClients.Add(name, httpClient);
        }

        public HttpClient CreateClient(string name)
        {
            return _httpClients[name];
        }
    }
}
