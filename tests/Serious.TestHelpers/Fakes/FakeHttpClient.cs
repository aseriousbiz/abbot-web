using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Serious.TestHelpers
{
    public class FakeHttpClient : HttpClient
    {
        readonly FakeHttpMessageHandler _fakeHttpMessageHandler;

        public FakeHttpClient() : this(new FakeHttpMessageHandler())
        {
        }

        public FakeHttpClient(FakeHttpMessageHandler fakeHttpMessageHandler)
            : base(fakeHttpMessageHandler)
        {
            _fakeHttpMessageHandler = fakeHttpMessageHandler;
        }

        public FakeHttpClient AddResponse(Uri url, HttpResponseMessage responseMessage)
        {
            _fakeHttpMessageHandler.AddResponse(url, responseMessage);
            return this;
        }

        public FakeHttpClient AddResponse(Uri url, HttpMethod httpMethod, HttpResponseMessage responseMessage)
        {
            _fakeHttpMessageHandler.AddResponse(url, httpMethod, responseMessage);
            return this;
        }

        public FakeHttpClient AddResponseException(Uri url, HttpMethod httpMethod, Exception exception)
        {
            _fakeHttpMessageHandler.AddResponseException(url, httpMethod, exception);
            return this;
        }

        public FakeHttpClient AddResponse<TResponseBody>(Uri url, HttpMethod httpMethod, TResponseBody responseBody)
        {
            _fakeHttpMessageHandler.AddResponse(url, httpMethod, responseBody);
            return this;
        }

        public FakeHttpClient AddStreamResponse(Func<HttpRequestMessage, Task<bool>> requestPredicate,
            Stream responseStream)
        {
            _fakeHttpMessageHandler.AddStreamResponse(requestPredicate, responseStream);
            return this;
        }
    }
}
