using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.Compilation;
using Serious.Abbot.Messages;
using Serious.Logging;

namespace Serious.Abbot.Controllers;

/// <summary>
/// API for C# skills to retrieve compiled code
/// </summary>
public class CompilationController : SkillRunnerApiControllerBase
{
    static readonly ILogger<CompilationController> Log = ApplicationLoggerFactory.CreateLogger<CompilationController>();

    readonly ICachingCompilerService _cachingCompilerService;

    public CompilationController(ICachingCompilerService cachingCompilerService)
    {
        _cachingCompilerService = cachingCompilerService;
    }

    [HttpPost("compilation")]
    public async Task<IActionResult> GetAssemblyAsync([FromBody] CompilationRequest compilationRequest)
    {
        // The Skill Editor allows calling a code in progress that haven't been saved yet.
        var unsavedChanges = Skill.CacheKey != compilationRequest.CacheKey;

        var stream = await _cachingCompilerService.GetCachedAssemblyStreamAsync(compilationRequest);

        if (stream == Stream.Null)
        {
            if (compilationRequest.Type == CompilationRequestType.Symbols)
            {
                return File(Stream.Null, "application/octet-stream");
            }

            if (unsavedChanges)
            {
                throw new InvalidOperationException($"The stream is empty for skill `{Skill.Name}`" +
                                                    $"(Id: {Skill.Id}, CacheKey: {Skill.CacheKey}) for compilation CacheKey:"
                                                    +
                                                    $" {compilationRequest.CacheKey}.");
            }

            var compilationResult = await _cachingCompilerService.CompileAsync(
                compilationRequest,
                Skill.Language,
                Skill.Code);

            if (compilationResult.CompilationErrors.Any())
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    compilationResult.CompilationErrors);
            }

#pragma warning disable CA2000
            stream = new MemoryStream();
#pragma warning restore
            try
            {
                await compilationResult.CompiledSkill.EmitAsync(stream, Stream.Null);
                stream.Position = 0;
            }
            catch (Exception e)
            {
                Log.ExceptionEmittingAssemblyStreams(e,
                    compilationRequest.SkillName,
                    compilationRequest.CacheKey,
                    compilationRequest.PlatformId,
                    compilationRequest.PlatformType);

                throw;
            }
        }

        return File(stream, "application/octet-stream");
    }
}
