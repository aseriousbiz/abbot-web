using System.Collections.Immutable;

namespace Serious.Abbot.Compilation;

public class SkillCompilationResult : ICompilationResult
{
    public SkillCompilationResult(ISkillCompilation compiledSkill)
        : this(compiledSkill, ImmutableList<ICompilationError>.Empty)
    {
    }

    public SkillCompilationResult(
        ISkillCompilation compiledSkill,
        IImmutableList<ICompilationError> compilationErrors)
    {
        CompiledSkill = compiledSkill;
        CompilationErrors = compilationErrors;
    }

    public ISkillCompilation CompiledSkill { get; }

    public IImmutableList<ICompilationError> CompilationErrors { get; }
}
