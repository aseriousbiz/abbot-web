using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Execution;
using Serious.Abbot.Functions.Execution;

namespace Serious.TestHelpers
{
    public class FakeCompiledSkillRunner : ICompiledSkillRunner
    {
        Dictionary<ICompiledSkill, ObjectResult> _results = new();

        public void AddObjectResult(ICompiledSkill compiledSkill, ObjectResult result)
        {
            _results.Add(compiledSkill, result);
        }

        public Task<ObjectResult> RunAndGetActionResultAsync(ICompiledSkill compiledSkill)
        {
            return Task.FromResult(_results[compiledSkill]);
        }
    }
}
