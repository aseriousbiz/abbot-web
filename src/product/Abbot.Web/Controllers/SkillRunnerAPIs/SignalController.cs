using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Messages;
using Serious.Abbot.Pages.Skills.Subscriptions;
using Serious.Abbot.Signals;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Controller responsible for raising signals from skills.
/// </summary>
public partial class SignalController : SkillRunnerApiControllerBase
{
    static readonly Regex SignalNameRegex = SignalNameValidatorRegex();

    readonly ISignalHandler _signalHandler;

    public SignalController(ISignalHandler signalHandler)
    {
        _signalHandler = signalHandler;
    }

    /// <summary>
    /// Enqueues a signal to be handled and immediately returns.
    /// </summary>
    /// <param name="signalRequest">The <see cref="SignalRequest"/> containing information about the signal to create.</param>
    /// <returns>An <see cref="IActionResult"/> with the result of the request.</returns>
    [HttpPost("signal")]
    public IActionResult PostAsync([FromBody] SignalRequest signalRequest)
    {
        if (signalRequest.Name.Length is 0)
        {
            return BadRequest(new ApiResult("Empty signal name is not allowed."));
        }

        if (!SignalNameRegex.IsMatch(signalRequest.Name))
        {
            return BadRequest(new ApiResult($"Signal name `{signalRequest.Name}` is not valid. Signal names may only contain a-z and 0-9. For multi-word names, separate the words by a dash character."));
        }

        if (signalRequest.Name.StartsWith(SystemSignal.Prefix, StringComparison.Ordinal))
        {
            return BadRequest(new ApiResult($"Signal name `{signalRequest.Name}` is reserved and cannot be raised by a user-defined skill."));
        }

        return _signalHandler.EnqueueSignalHandling(Skill, signalRequest)
            ? Ok(new ApiResult())
            : BadRequest(new ApiResult($"Signal `{signalRequest.Name}` would result in a signal cycle."));
    }

    [GeneratedRegex(SignalSubscriptionInputModel.SignalNamePattern, RegexOptions.Compiled)]
    private static partial Regex SignalNameValidatorRegex();
}
