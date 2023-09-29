using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Compilation;
using Serious.Abbot.Entities;

namespace Serious.TestHelpers
{
    public class FakeSkillCompiler : ISkillCompiler
    {
        readonly Dictionary<string, ICompilationResult> _compilationResults = new Dictionary<string, ICompilationResult>();

        public void AddCompilationResult(string code, ICompilationResult script)
        {
            _compilationResults.Add(code, script);
        }

        public Task<ICompilationResult> CompileAsync(CodeLanguage language, string code)
        {
            return Task.FromResult(_compilationResults.TryGetValue(code, out var compiled)
                ? compiled
                : new FakeSkillCompilationResult(code));
        }
    }
}
