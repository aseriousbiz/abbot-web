using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Recompiles .NET skills when the cache key and the code do not match. We can trigger a full recompile of
/// all skills by changing <see cref="WebConstants.CodeCacheKeyHashSeed"/>.
/// </summary>
public sealed class SkillRecompilationSeeder : IDataSeeder
{
    static readonly ILogger<SkillRecompilationSeeder> Log = ApplicationLoggerFactory.CreateLogger<SkillRecompilationSeeder>();

    readonly AbbotContext _db;

    public SkillRecompilationSeeder(AbbotContext db)
    {
        _db = db;
    }

    public async Task SeedDataAsync()
    {
        var skills = await _db.Skills
            .Where(s => s.Language == CodeLanguage.CSharp)
            .ToListAsync();

        var needRecompilation = skills
            .Select(skill => new { Skill = skill, NewCacheKey = SkillCompiler.ComputeCacheKey(skill.Code) })
            .Where(info => !info.Skill.CacheKey.Equals(info.NewCacheKey, StringComparison.Ordinal))
            .Select(info => {
                info.Skill.CacheKey = info.NewCacheKey;
                return info.Skill;
            })
            .ToList();

        if (needRecompilation is { Count: 0 })
        {
            Log.NoRecompilationNeeded();
        }
        else
        {
            Log.RecompilationNeeded(needRecompilation.Count);
        }

        await _db.SaveChangesAsync();
    }

    public bool Enabled => true;
}

public static partial class SkillRecompilationSeederLoggingExtensions
{
    [LoggerMessage(
        EventId = 70,
        Level = LogLevel.Information,
        Message = "No skills need recompilation")]
    public static partial void NoRecompilationNeeded(this ILogger<SkillRecompilationSeeder> logger);

    [LoggerMessage(
        EventId = 3100,
        Level = LogLevel.Warning,
        Message = "Updating the cache key for {Count} skills!")]
    public static partial void RecompilationNeeded(this ILogger<SkillRecompilationSeeder> logger, int count);
}
