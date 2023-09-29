using Microsoft.AspNetCore.Http;
using Serious.Abbot.Functions.Runtime;

namespace Serious.Abbot.Functions.Execution;

/// <summary>
/// Provides access to the current <see cref="SkillContext"/>, if one is available. This is inspired by the <see cref="IHttpContextAccessor"/> interface.
/// </summary>
/// <remarks>
/// This class must be registered in DI scoped to the request (aka AddScoped).
/// </remarks>
public class SkillContextAccessor : ISkillContextAccessor
{
    /// <summary>
    /// Gets or sets the current <see cref="SkillContext"/>. Returns <see langword="null" /> if there is no active <see cref="SkillContext" />.
    /// </summary>
    public SkillContext? SkillContext { get; set; }
}
