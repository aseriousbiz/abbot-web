using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Newtonsoft.Json;
using Serious.Abbot.Serialization;

namespace Serious.TestHelpers
{
    public class FakeHttpRequestData : HttpRequestData
    {
        public FakeHttpRequestData() : this(new FakeFunctionContext())
        {
        }

        public FakeHttpRequestData(FunctionContext functionContext)
            : this(functionContext,
                new MemoryStream(),
                new HttpHeadersCollection(),
                new List<IHttpCookie>(),
                new Uri("https://localhost:7071/SkillRunner"),
                Enumerable.Empty<ClaimsIdentity>(),
                "POST")
        {
        }

        public FakeHttpRequestData(string httpMethod, IDictionary<string, string> headers)
            : this(new FakeFunctionContext(),
                new MemoryStream(),
                HeadersFromDictionary(headers),
                new List<IHttpCookie>(),
                new Uri("https://localhost:7071/SkillRunner"),
                Enumerable.Empty<ClaimsIdentity>(),
                httpMethod)
        {
        }

        public FakeHttpRequestData(string httpMethod, IDictionary<string, string> headers, Stream body)
            : this(new FakeFunctionContext(),
                body,
                HeadersFromDictionary(headers),
                new List<IHttpCookie>(),
                new Uri("https://localhost:7071/SkillRunner"),
                Enumerable.Empty<ClaimsIdentity>(),
                httpMethod)
        {
        }

        public FakeHttpRequestData(
            FunctionContext functionContext,
            Stream body,
            HttpHeadersCollection headers,
            IReadOnlyCollection<IHttpCookie> cookies,
            Uri url,
            IEnumerable<ClaimsIdentity> identities,
            string method)
            : base(functionContext)
        {
            Body = body;
            Headers = headers;
            Cookies = cookies;
            Url = url;
            Identities = identities;
            Method = method;
        }

        public override HttpResponseData CreateResponse()
        {
            return new FakeHttpResponseData(FunctionContext);
        }

        public override Stream Body { get; }
        public override HttpHeadersCollection Headers { get; }
        public override IReadOnlyCollection<IHttpCookie> Cookies { get; }
        public override Uri Url { get; }
        public override IEnumerable<ClaimsIdentity> Identities { get; }
        public override string Method { get; }

        static HttpHeadersCollection HeadersFromDictionary(IDictionary<string, string> headers)
        {
            return new(
                headers.Select(kvp => new KeyValuePair<string, IEnumerable<string>>(kvp.Key, new[] { kvp.Value })));
        }

        public static Task<FakeHttpRequestData> CreateAsync(object body)
        {
            return CreateAsync(body, new Dictionary<string, string>());
        }

        public static async Task<FakeHttpRequestData> CreateAsync(object body, IDictionary<string, string> headers)
        {
            var stream = new MemoryStream();
            var bodyStream = new MemoryStream();
            await using var sw = new StreamWriter(stream);
            var serialized = AbbotJsonFormat.Default.Serialize(body);
            await sw.WriteAsync(serialized);
            await sw.FlushAsync();
            stream.Position = 0;
            await stream.CopyToAsync(bodyStream);
            bodyStream.Position = 0;
            return new FakeHttpRequestData(HttpMethods.Post, headers, bodyStream);
        }
    }
}
