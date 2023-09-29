using System;
using System.Collections.Generic;
using System.Linq;
using CronExpressionDescriptor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Serious.Abbot.Controllers.InternalApi;

[Route("api/internal/cron")]
public class CronController : InternalApiControllerBase
{
    static readonly HashSet<(string, string)> NeverLookup = new HashSet<(string, string)>
    {
        ("30", "2"), // Feb 30
        ("31", "2"), // Feb 31
        ("31", "4"), // Apr 31
        ("31", "6"), // Jun 31
        ("31", "9"), // Sep 31
        ("31", "11") // Nov 31
    };

    /// <summary>
    /// Retrieve a description for a cron statement.
    /// </summary>
    /// <param name="cron"></param>
    [HttpGet]
    [ProducesResponseType(typeof(CronResult), StatusCodes.Status200OK)]
    public IActionResult Get(string cron)
    {
        try
        {
            var options = new Options
            {
                ThrowExceptionOnParseError = true,
                Verbose = true
            };
            var parser = new ExpressionParser(cron, options);
            var segments = parser.Parse();
            if (segments.Length != 7)
            {
                throw new InvalidOperationException(
                    $"Cron parser did not return 7 segments. It returned {segments.Length} for `{cron}`");
            }
            if (!string.IsNullOrEmpty(segments[0]) || !string.IsNullOrEmpty(segments.Last()))
            {
                return Json(
                    new CronResult(false, "Only five part cron expressions are supported"));
            }

            var minutes = segments[1];
            var slashPos = minutes.IndexOf('/', StringComparison.Ordinal);

            var everyMinute = slashPos > -1
                ? int.TryParse(minutes[(slashPos + 1)..], out var minute) ? minute : 10
                : 10;

            if (minutes == "*" || minutes.Contains('-', StringComparison.Ordinal) || everyMinute < 10)
            {
                return Json(new CronResult(false, "Skills that run more than every ten minutes are not allowed"));
            }

            var day = segments[3];
            var month = segments[4];

            var description = NeverLookup.Contains((day, month))
                ? "Never"
                : ExpressionDescriptor.GetDescription(cron, options);
            return Json(new CronResult(true, description));
        }
        catch (FormatException e)
        {
            return Json(new CronResult(false, e.Message));
        }
    }
}

public class CronResult
{
    public CronResult(bool success, string description)
    {
        Success = success;
        Description = description;
    }

    public bool Success { get; }
    public string Description { get; }
}
