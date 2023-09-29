using System.Collections.Immutable;

namespace Serious.Abbot.Compilation;

/// <summary>
/// A result of compiling skill code.
/// </summary>
public interface ICompilationResult
{
    /// <summary>
    /// The compiled skill assembly.
    /// </summary>
    ISkillCompilation CompiledSkill { get; }

    /// <summary>
    /// The set of compilation errors, if any.
    /// </summary>
    IImmutableList<ICompilationError> CompilationErrors { get; }
}
