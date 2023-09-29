using System.Collections.Generic;
using System.Linq;
using Humanizer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Controllers.InternalApi;
using Serious.Abbot.Controllers.PublicApi;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.Messages;
using Serious.Abbot.Messages.Models;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Routing;
using Serious.Logging;

namespace Serious.Abbot.Controllers;

/// <summary>
/// Base class for controllers that run skills such as the <see cref="CliController"/>
/// and <see cref="SkillEditorController"/>.
/// </summary>
/// <remarks>
/// This is only used to run skills outside of the chat context, hence no message Ids or thread Ids.
/// </remarks>
public abstract class SkillRunControllerBase : UserControllerBase
{
    static readonly ILogger<SkillRunControllerBase> Log = ApplicationLoggerFactory.CreateLogger<SkillRunControllerBase>();

    readonly ISkillRunnerClient _skillRunnerClient;
    readonly IUserRepository _userRepository;
    readonly IRoomRepository _roomRepository;
    readonly ICachingCompilerService _cachingCompilerService;
    readonly IPermissionRepository _permissions;

    protected IUrlGenerator UrlGenerator { get; }

    protected SkillRunControllerBase(
        ISkillRunnerClient skillRunnerClient,
        IUserRepository userRepository,
        IRoomRepository roomRepository,
        ICachingCompilerService cachingCompilerService,
        IPermissionRepository permissions,
        IUrlGenerator urlGenerator)
    {
        _skillRunnerClient = skillRunnerClient;
        _userRepository = userRepository;
        _roomRepository = roomRepository;
        _cachingCompilerService = cachingCompilerService;
        _permissions = permissions;
        UrlGenerator = urlGenerator;
    }

    /// <summary>
    /// Sends a request to run a skill to the appropriate skill runner.
    /// </summary>
    /// <param name="skill">The <see cref="Skill"/> to run.</param>
    /// <param name="actor">The <see cref="Member"/> who is calling the skill.</param>
    /// <param name="runRequest">The request to run the skill.</param>
    /// <param name="room">The room the skill should respond to.</param>
    /// <param name="signal">Information about the signal that's causing this skill to be called.</param>
    /// <returns>An <see cref="IActionResult"/> with the result of the request.</returns>
    protected async Task<IActionResult> SendSkillRunRequestAsync(
        Skill skill,
        Member actor,
        SkillRunRequest runRequest,
        PlatformRoom room,
        SignalMessage? signal = null)
    {
        Log.SkillMethodEntered(GetType(), nameof(SendSkillRunRequestAsync), skill.Id, skill.Name);
        using var _ = Log.BeginSkillScope(skill);

        if (skill.IsDeleted)
        {
            return NotFound();
        }

        if (!await _permissions.CanEditAsync(actor, skill))
        {
            var error = new ErrorResponse
            {
                Message = $"You do not have permission to run this skill. " +
                          $"In {actor.Organization.PlatformType.Humanize()}, you can run " +
                          $"`@{skill.Organization.BotName} who can {skill.Name}` to find out who can change permissions for this skill."
            };

            return StatusCode(StatusCodes.Status403Forbidden, error);
        }

        var temporarySkill = skill.CopyInstanceWithNewNameAndCode(runRequest.Name, runRequest.Code);
        var mentions = await _userRepository.ParseMentions(runRequest.Arguments, actor.Organization);

        if (skill.Language is CodeLanguage.CSharp or CodeLanguage.Ink)
        {
            var exists = await _cachingCompilerService.ExistsAsync(actor.Organization, temporarySkill.Code);

            if (!exists)
            {
                // Compiles and save code to the cache.
                var compilation = await _cachingCompilerService.CompileAsync(
                    actor.Organization, skill.Language,
                    temporarySkill.Code);
                if (compilation.CompilationErrors.Any())
                {
                    var compilationErrorResponse = new CompilerErrorResponse
                    {
                        Message = "Errors occurred attempting to compile the skill code",
                        Errors = compilation.CompilationErrors.Select(e => new CompilationError
                        {
                            ErrorId = e.ErrorId,
                            Description = e.Description,
                            LineStart = e.LineStart,
                            LineEnd = e.LineEnd,
                            SpanStart = e.SpanStart,
                            SpanEnd = e.SpanEnd
                        }).ToList()
                    };

                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        compilationErrorResponse);
                }
            }
        }

        Log.SendingAbbotCliRequest(
            skill.Id,
            temporarySkill.Name,
            temporarySkill.CacheKey,
            runRequest.Arguments,
            skill.Organization.PlatformId,
            skill.Organization.PlatformType);

        var dbRoom = await _roomRepository.GetRoomByPlatformRoomIdAsync(room.Id, skill.Organization);

        try
        {
            var skillUrl = UrlGenerator.SkillPage(skill.Name);
            var response = await _skillRunnerClient.SendAsync(
                temporarySkill,
                new Arguments(
                    runRequest.Arguments,
                    mentions.Select(m => m.ToPlatformUser())),
                $"{temporarySkill} {runRequest.Arguments}",
                mentions,
                actor,
                BotChannelUser.GetBotUser(actor.Organization),
                room,
                dbRoom?.Customer?.ToCustomerInfo(),
                skillUrl,
                signal: signal,
                passiveReplies: true,
                auditProperties: new()
                {
                    CommandText = $"{temporarySkill} {runRequest.Arguments}",
                });

            return response.Success
                ? StatusCode(StatusCodes.Status200OK, response)
                : StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new RuntimeErrorResponse
                    {
                        Message = "Runtime errors occurred calling the skill",
                        Errors = response.Errors ?? Array.Empty<RuntimeError>()
                    });
        }
        catch (Exception)
        {
            var errors = new List<RuntimeError>
            {
                new()
                {
                    Description = WebConstants.UnexpectedBotErrorMessage,
                    ErrorId = "Exception"
                }
            };
            var exceptionResponse = new RuntimeErrorResponse
            {
                Message = $"Unexpected exception calling the skill runner for skill {skill.Name}",
                Errors = errors
            };
            return StatusCode(StatusCodes.Status500InternalServerError, exceptionResponse);
        }
    }
}
