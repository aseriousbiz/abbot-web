using System;
using System.Net.Http;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Functions.Storage;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Functions.Clients;

/// <summary>
/// Not to be confused with SignalR, use this to raise signals by calling the
/// signal api endpoint /api/skills/{id}/signal
/// </summary>
public class Signaler : ISignaler
{
    readonly ISkillApiClient _apiClient;
    readonly ISkillContextAccessor _skillContextAccessor;

    /// <summary>
    /// Constructs a <see cref="Signaler"/>.
    /// </summary>
    /// <param name="skillApiClient">Client to call the skill runner APIs.</param>
    /// <param name="skillContextAccessor">A <see cref="ISkillContextAccessor"/> used to access the current <see cref="SkillContext"/>.</param>
    public Signaler(ISkillApiClient skillApiClient, ISkillContextAccessor skillContextAccessor)
    {
        _apiClient = skillApiClient;
        _skillContextAccessor = skillContextAccessor;
    }

    /// <summary>
    /// Raises a signal from the skill with the specified name and arguments.
    /// </summary>
    /// <param name="name">The name of the signal.</param>
    /// <param name="arguments">The arguments to pass to the skills that are subscribed to this signal.</param>
    /// <returns>A <see cref="IResult"/> that indicates whether the operation succeeded or not.</returns>
    public async Task<IResult> SignalAsync(string name, string arguments)
    {
        bool isRoot = SkillContext.SignalInfo is null;

        var signalRequest = new SignalRequest
        {
            Name = name,
            Arguments = arguments,
            Room = SkillInfo.Room,
            SenderId = SkillRunnerInfo.MemberId,
            ConversationId = SkillContext.ConversationInfo?.Id,
            Source = new SignalSourceMessage
            {
                AuditIdentifier = SkillRunnerInfo.AuditIdentifier,
                SkillName = SkillInfo.SkillName,
                SkillUrl = SkillInfo.SkillUrl,
                Arguments = SkillInfo.Arguments,
                Mentions = SkillInfo.Mentions,
                SignalEvent = SkillContext.SignalInfo,

                // Only set these if this is a root source (aka isRoot = true)
                IsChat = isRoot ? SkillInfo.IsChat : null,
                IsInteraction = isRoot ? SkillInfo.IsInteraction : null,
                IsPatternMatch = isRoot ? SkillInfo.Pattern is not null : null,
                IsRequest = isRoot ? SkillInfo.IsRequest : null,
                Pattern = SkillInfo.Pattern, // This could only be populated by a root skill source.
                Request = SkillInfo.Request, // This could only be populated by a root skill source.
            }
        };

        IResult? response = await _apiClient.SendJsonAsync<SignalRequest, ApiResult>(
            SignalsApiUrl,
            HttpMethod.Post,
            signalRequest);

        return new Result(response ?? new Result("unknown error occurred"));
    }

    SkillInfo SkillInfo => SkillContext.SkillInfo;
    SkillRunnerInfo SkillRunnerInfo => SkillContext.SkillRunnerInfo;

    SkillContext SkillContext => _skillContextAccessor.SkillContext
        ?? throw new InvalidOperationException($"The {nameof(SkillContext)} needs to be set for this request.");

    Uri SignalsApiUrl => _apiClient.BaseApiUrl.Append("/signal");
}
