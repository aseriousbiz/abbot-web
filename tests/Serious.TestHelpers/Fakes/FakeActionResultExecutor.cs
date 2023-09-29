using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;

namespace Serious.TestHelpers;

public class FakeActionResultExecutor<TResult> : IActionResultExecutor<TResult> where TResult : IActionResult
{
    public async Task ExecuteAsync(ActionContext context, TResult result)
    {
        var response = context.HttpContext.Response;
        if (result is ContentResult contentResult)
        {
            response.StatusCode = contentResult.StatusCode.GetValueOrDefault(200);
            response.ContentType = contentResult.ContentType;
            await response.Body.WriteStringAsync(contentResult.Content);
        }
    }
}

public class FakeContentResultExecutor : ContentResultExecutor
{
    public FakeContentResultExecutor()
        : base(NullLogger<ContentResultExecutor>.Instance, new FakeHttpResponseStreamWriterFactory())
    {
    }
}

// Unfortunately, NewtonsoftJsonResultExecutor is internal, so we fake it.
public class FakeJsonResultExecutor : IActionResultExecutor<JsonResult>
{
    public async Task ExecuteAsync(ActionContext context, JsonResult result)
    {
        var json = JsonConvert.SerializeObject(result.Value);
        await new ContentResult
        {
            Content = json,
            ContentType = "application/json",
        }.ExecuteResultAsync(context);
    }
}

public class FakeHttpResponseStreamWriterFactory : IHttpResponseStreamWriterFactory
{
    public TextWriter CreateWriter(Stream stream, Encoding encoding)
    {
        return new NonDisposingStreamWriter(stream, encoding);
    }
}

public class NonDisposingStreamWriter : StreamWriter
{
    public NonDisposingStreamWriter(Stream stream, Encoding encoding) : base(stream, encoding)
    {
    }

    protected override void Dispose(bool disposing)
    {
        // Don't dispose it.
    }
}

public class FakeArrayPool<T> : ArrayPool<T>
{
    public override T[] Rent(int minimumLength)
    {
        return new T[minimumLength];
    }

    public override void Return(T[] array, bool clearArray = false)
    {
    }
}
