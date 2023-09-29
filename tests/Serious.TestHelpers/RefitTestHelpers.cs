using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Refit;

namespace Serious.TestHelpers;

public static class RefitTestHelpers
{
    public static ApiException CreateApiException(
        HttpStatusCode statusCode,
        HttpMethod method,
        string uri,
        object payload)
    {
        var json = JsonConvert.SerializeObject(payload);
        var req = new HttpRequestMessage(method, uri);
        var resp = new HttpResponseMessage(statusCode);
        resp.Content = new StringContent(json);

        // ApiException.Create is only async because it reads the HttpResponseContent.
        // We put a string in though, so we know it won't go async.
        // A little clunky, but this is test code, what's a little clunkiness between friends?
        return ApiException.Create(req, req.Method, resp, new RefitSettings()).Result;
    }
}
