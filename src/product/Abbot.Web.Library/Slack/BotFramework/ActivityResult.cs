using System;
using System.Globalization;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace Serious.Slack.BotFramework;

/// <summary>
/// An <see cref="IActionResult"/> used to handle an incoming Bot Framework <see cref="Activity"/> and run the
/// Bot Framework pipeline with the Activity.
/// </summary>
public class ActivityResult : IActionResult
{
    /// <summary>
    /// When setting the response action or a JSON response body, use this key in the TurnState.
    /// </summary>
    public const string ResponseBodyKey = nameof(ResponseBodyKey);

    static readonly JsonSerializerSettings SerializerSettings = new()
    {
        // Slack can be picky about returning null/default fields.
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    readonly Func<ITurnContext, Task> _runPipeline;

    public ActivityResult(ITurnContext turnContext, Func<ITurnContext, Task> runPipeline)
    {
        TurnContext = turnContext;
        _runPipeline = runPipeline;
    }

    /// <summary>
    /// The Activity to handle.
    /// </summary>
    public ITurnContext TurnContext { get; }

    /// <summary>
    /// Runs the Bot Framework pipeline and writes a response.
    /// </summary>
    /// <param name="context">The Action context.</param>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        TurnContext.TurnState.Add("httpStatus", ((int)HttpStatusCode.OK).ToString(CultureInfo.InvariantCulture));

        await _runPipeline(TurnContext).ConfigureAwait(false);

        if (TurnContext.TurnState.TryGetValue(ResponseBodyKey, out var responseBody))
        {
            var jsonResult = new JsonResult(responseBody, SerializerSettings);
            await jsonResult.ExecuteResultAsync(context);
            return;
        }

        var code = Convert.ToInt32(TurnContext.TurnState.Get<string>("httpStatus"), CultureInfo.InvariantCulture);
        var statusCode = (HttpStatusCode)code;
        var text = TurnContext.TurnState.Get<object>("httpBody") != null
            ? TurnContext.TurnState.Get<object>("httpBody").ToString()
            : string.Empty;

        var contentResult = new ContentResult
        {
            Content = text,
            StatusCode = (int)statusCode,
            ContentType = "text/plain"
        };

        await contentResult.ExecuteResultAsync(context);
    }
}
