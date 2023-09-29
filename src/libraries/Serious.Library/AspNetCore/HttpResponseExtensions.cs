using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Serious.AspNetCore;

public static class HttpResponseExtensions
{
    // * CREDIT: adapted from https://stackoverflow.com/questions/42000362/creating-a-proxy-to-another-web-api-with-asp-net-core/62339908#62339908 with minor style changes.
    public static async Task CopyProxyHttpResponseAsync(this HttpResponse response, HttpResponseMessage? responseMessage)
    {
        if (responseMessage is null)
        {
            throw new ArgumentNullException(nameof(responseMessage));
        }

        response.StatusCode = (int)responseMessage.StatusCode;
        foreach (var (key, value) in responseMessage.Headers)
        {
            response.Headers[key] = value.ToArray();
        }

        foreach (var (key, value) in responseMessage.Content.Headers)
        {
            response.Headers[key] = value.ToArray();
        }

        // SendAsync removes chunking from the response.
        // This removes the header so it doesn't expect a chunked response.
        response.Headers.Remove("transfer-encoding");
        await using var responseStream = await responseMessage.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(response.Body, response.HttpContext.RequestAborted);
    }
}
