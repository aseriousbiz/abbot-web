using System.IO;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Serious.TestHelpers
{
    public class FakeHttpResponseData : HttpResponseData
    {
        HttpStatusCode _statusCode;
        Stream _body = new MemoryStream();
        HttpHeadersCollection _headers = new HttpHeadersCollection();

        public FakeHttpResponseData(FunctionContext functionContext) : base(functionContext)
        {
            _statusCode = HttpStatusCode.OK;
        }

        public override HttpStatusCode StatusCode
        {
            get => _statusCode;
            set => _statusCode = value;
        }

        public override HttpHeadersCollection Headers
        {
            get => _headers;
            set => _headers = value;
        }

        public override Stream Body
        {
            get => _body;
            set => _body = value;
        }

        public override HttpCookies Cookies { get; } = null!;
    }
}
