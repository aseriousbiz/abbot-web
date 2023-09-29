using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Serious.Slack.AspNetCore;

public interface ISlackOptionsProvider
{
    Task<SlackOptions> GetOptionsAsync(HttpContext httpContext);
}

public class DefaultSlackOptionsProvider : ISlackOptionsProvider
{
    readonly IOptions<SlackOptions> _options;

    public DefaultSlackOptionsProvider(IOptions<SlackOptions> options)
    {
        _options = options;
    }

    public Task<SlackOptions> GetOptionsAsync(HttpContext httpContext) => Task.FromResult(_options.Value);
}
