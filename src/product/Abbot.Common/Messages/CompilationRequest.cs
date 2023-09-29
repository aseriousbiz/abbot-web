using Serious.Abbot.Entities;
using Serious.Abbot.Scripting;

namespace Serious.Abbot.Messages;

/// <summary>
/// Body of the request by a C# skill for a compiled skill assembly
/// </summary>
public class CompilationRequest : IOrganizationIdentifier
{
    public CompilationRequest()
    {
    }

    public CompilationRequest(IOrganizationIdentifier organizationIdentifier, string skillName, string cacheKey, CodeLanguage language)
    {
        SkillName = skillName;
        CacheKey = cacheKey;
        Language = language;
        PlatformId = organizationIdentifier.PlatformId;
        PlatformType = organizationIdentifier.PlatformType;
    }

    public CompilationRequest(ICompiledSkillIdentifier skillAssemblyIdentifier)
        : this(skillAssemblyIdentifier, skillAssemblyIdentifier.SkillName, skillAssemblyIdentifier.CacheKey, skillAssemblyIdentifier.Language)
    {
    }

    /// <summary>
    /// The name to use for the compiled skill.
    /// </summary>
    public string SkillName { get; init; } = null!;

    /// <summary>
    /// The cache key of the code to request. This is a hash of the code.
    /// </summary>
    public string CacheKey { get; init; } = null!;

    public CodeLanguage Language { get; }

    /// <summary>
    /// The Id of the chat platform this request is for.
    /// </summary>
    public string PlatformId { get; init; } = null!;

    /// <summary>
    /// The platform type such as slack or teams.
    /// </summary>
    public PlatformType PlatformType { get; init; }

    /// <summary>
    /// The type of compilation request
    /// </summary>
    public CompilationRequestType Type { get; init; }

    /// <summary>
    /// Copies this request but sets the type to a symbols request.
    /// </summary>
    public CompilationRequest ToSymbolsRequest()
    {
        return new()
        {
            SkillName = SkillName,
            CacheKey = CacheKey,
            PlatformId = PlatformId,
            PlatformType = PlatformType,
            Type = CompilationRequestType.Symbols
        };
    }
}
