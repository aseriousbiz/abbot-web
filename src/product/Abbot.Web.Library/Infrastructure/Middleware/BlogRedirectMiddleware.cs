using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Serious.Abbot.Infrastructure.Middleware;

public class BlogRedirectMiddleware
{
    readonly RequestDelegate _next;
    readonly IHostEnvironment _hostEnvironment;

    public BlogRedirectMiddleware(RequestDelegate next, IHostEnvironment hostEnvironment)
    {
        _next = next;
        _hostEnvironment = hostEnvironment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Host.Host.Equals("blog.ab.bot", StringComparison.OrdinalIgnoreCase)
            || (_hostEnvironment.IsDevelopment() && context.Request.Query["blog"].ToString() == "true")
        )
        {
            context.Response.Redirect("https://ab.bot/blog");
        }

        // Move forward into the pipeline
        await _next(context);
    }
}
