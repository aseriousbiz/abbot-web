using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Execution;

namespace Serious.Abbot.Functions.Execution;

/// <summary>
/// Runs a compiled skill and returns an <see cref="ObjectResult"/>.
/// </summary>
public interface ICompiledSkillRunner
{
    /// <summary>
    /// Run the compiled skill and return the object result.
    /// </summary>
    /// <param name="compiledSkill">The compiled skill to run.</param>
    /// <returns></returns>
    Task<ObjectResult> RunAndGetActionResultAsync(ICompiledSkill compiledSkill);
}
