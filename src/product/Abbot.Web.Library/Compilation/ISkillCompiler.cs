using System.Threading.Tasks;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Compilation;

/// <summary>
/// A skill code compiler.
/// </summary>
public interface ISkillCompiler
{
    /// <summary>
    /// Compiles the specified code into a compilation result.
    /// </summary>
    /// <param name="language">The language of the code.</param>
    /// <param name="code">The code to compile.</param>
    Task<ICompilationResult> CompileAsync(CodeLanguage language, string code);
}
