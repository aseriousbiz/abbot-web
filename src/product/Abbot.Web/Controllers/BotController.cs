// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.6.2

using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Logging;
using IBot = Microsoft.Bot.Builder.IBot;

namespace Serious.Abbot.Controllers;

// This ASP Controller is created to handle a request. Dependency Injection will provide the Adapter and IBot
// implementation at runtime. Multiple different IBot implementations running at different endpoints can be
// achieved by specifying a more specific type for the bot constructor argument.
[AllowAnonymous]
[Route("api/messages")]
[ApiController]
[AbbotWebHost]
public class BotController : ControllerBase
{
    static readonly ILogger<BotController> Log = ApplicationLoggerFactory.CreateLogger<BotController>();

    readonly IBotFrameworkHttpAdapter _adapter;
    readonly IBot _bot;

    public BotController(IBotFrameworkHttpAdapter adapter, IBot bot)
    {
        _adapter = adapter;
        _bot = bot;
    }

    [HttpPost, HttpGet]
    public async Task PostAsync()
    {
        Log.MethodEntered(typeof(BotController), nameof(PostAsync), "Received Azure Bot Service Request");

        // Delegate the processing of the HTTP POST to the adapter.
        // The adapter will invoke the bot.
        await _adapter.ProcessAsync(Request, Response, _bot);
    }
}
