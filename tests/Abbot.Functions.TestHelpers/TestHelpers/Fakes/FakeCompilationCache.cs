using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions.Cache;
using Serious.Abbot.Messages;

namespace Serious.TestHelpers
{
    public class FakeCompilationCache : ICompilationCache
    {
        readonly Dictionary<string, ICompiledSkill> _compiledSkills = new();

        public void Add(string cacheKey, ICompiledSkill compiledSkill)
        {
            _compiledSkills.Add(cacheKey, compiledSkill);
        }

        public Task<ICompiledSkill> GetCompiledSkillAsync(ICompiledSkillIdentifier skillAssemblyIdentifier)
        {
            return _compiledSkills.TryGetValue(skillAssemblyIdentifier.CacheKey, out var skill)
                ? Task.FromResult(skill)
                : Task.FromResult((ICompiledSkill)null!);
        }
    }
}
