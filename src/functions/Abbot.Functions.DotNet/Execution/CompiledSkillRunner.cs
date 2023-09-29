using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Execution;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Logging;

namespace Serious.Abbot.Functions.Execution;

/// <summary>
/// Runs a compiled skill and returns an <see cref="ObjectResult"/>.
/// </summary>
public class CompiledSkillRunner : ICompiledSkillRunner
{
    readonly IExtendedBot _bot;

    static readonly ILogger<CompiledInkScript> Log = ApplicationLoggerFactory.CreateLogger<CompiledInkScript>();

    /// <summary>
    /// Constructs a <see cref="CompiledSkillRunner"/>.
    /// </summary>
    /// <param name="bot">The bot to pass to the skills. This is one is a wrapper interface so we can extract replies.</param>
    public CompiledSkillRunner(IExtendedBot bot)
    {
        _bot = bot;
    }

    /// <summary>
    /// Run the compiled skill and return the object result.
    /// </summary>
    /// <param name="compiledSkill">The compiled skill to run.</param>
    /// <returns>The <see cref="ObjectResult"/> to return to the caller.</returns>
    public async Task<ObjectResult> RunAndGetActionResultAsync(ICompiledSkill compiledSkill)
    {
        try
        {
            var exception = await Log.LogElapsedAsync("RunSkill", () => compiledSkill.RunAsync(_bot));
            if (exception is not null)
            {
                ExceptionDispatchInfo.Throw(exception);
            }
        }
        catch (Exception e)
        {
            var errors = new List<RuntimeError> { RuntimeErrorFactory.Create(e.Unwrap(), _bot.SkillName) };
            var errorResponse = SkillRunResponseFactory.CreateFailed(errors);
            return new ObjectResult(ApplyHttpResponseSettings(_bot, errorResponse))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }

        var response = SkillRunResponseFactory.CreateSuccess(_bot.Replies, _bot.Outputs.ToReadOnlyDictionary());
        return new OkObjectResult(ApplyHttpResponseSettings(_bot, response));
    }

    static SkillRunResponse ApplyHttpResponseSettings(IBot bot, SkillRunResponse response)
    {
        if (bot.IsRequest)
        {
            var httpResponse = bot.Response;
            response.Content = httpResponse.RawContent;
            response.ContentType = httpResponse.ContentType;
            response.Headers = ToDictionary(httpResponse.Headers);
        }

        return response;
    }

    static Dictionary<string, string?[]> ToDictionary(IResponseHeaders headers)
    {
        return headers.ToDictionary(
            h => h.Key,
            h => h.Value.Select(v => v).ToArray());
    }
}
