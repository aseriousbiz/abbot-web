using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Serious.AspNetCore;

public class ProxyResult : IActionResult
{
    readonly HttpResponseMessage _responseMessage;

    public ProxyResult(HttpResponseMessage responseMessage)
    {
        _responseMessage = responseMessage;
    }

    public Task ExecuteResultAsync(ActionContext context)
    {
        return context.HttpContext.Response.CopyProxyHttpResponseAsync(_responseMessage);
    }
}
