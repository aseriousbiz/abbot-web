using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Serious.Abbot.Compilation;

namespace Serious.TestHelpers
{
    public class FakeSkillCompilationResult : ICompilationResult
    {
        public FakeSkillCompilationResult(string code)
            : this(new FakeSkillCompilation(code), Enumerable.Empty<ICompilationError>())
        {
        }

        public FakeSkillCompilationResult(params ICompilationError[] errors)
            : this(new FakeSkillCompilation(), errors)
        {
        }

        public FakeSkillCompilationResult(IEnumerable<ICompilationError> errors)
            : this(new FakeSkillCompilation(), errors)
        {
        }

        public FakeSkillCompilationResult(ISkillCompilation skillCompilation)
            : this(skillCompilation, Enumerable.Empty<ICompilationError>())
        {
        }

        public FakeSkillCompilationResult(
            ISkillCompilation skillCompilation,
            IEnumerable<ICompilationError> errors)
        {
            CompiledSkill = skillCompilation;
            CompilationErrors = errors.ToImmutableList();
        }

        public ISkillCompilation CompiledSkill { get; }

        public IImmutableList<ICompilationError> CompilationErrors { get; }
    }
}
