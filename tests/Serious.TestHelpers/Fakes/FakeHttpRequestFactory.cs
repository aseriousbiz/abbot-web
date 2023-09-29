using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Serious.TestHelpers
{
    public static class FakeHttpRequestFactory
    {
        public static async Task<HttpRequest> CreateAsync(object body)
        {
            var request = new DefaultHttpContext().Request;
            await WriteBodyToRequest(request, body);
            return request;
        }

        public static async Task WriteBodyToRequest(HttpRequest request, object body)
        {
            var stream = new MemoryStream();
            var bodyStream = new MemoryStream();
            await using var sw = new StreamWriter(stream);
            var serialized = JsonConvert.SerializeObject(body);
            await sw.WriteAsync(serialized);
            await sw.FlushAsync();
            stream.Position = 0;
            await stream.CopyToAsync(bodyStream);
            bodyStream.Position = 0;
            request.Method = HttpMethods.Post;
            request.Body = bodyStream;
        }
    }
}
