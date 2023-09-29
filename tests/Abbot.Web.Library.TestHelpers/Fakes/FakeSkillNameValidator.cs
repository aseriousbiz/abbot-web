using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Validation;

namespace Serious.TestHelpers
{
    public class FakeSkillNameValidator : ISkillNameValidator
    {
        readonly Dictionary<string, UniqueNameResult> _results = new();

        public Task<UniqueNameResult> IsUniqueNameAsync(string name, int id, string type, Organization organization)
        {
            return Task.FromResult(_results.TryGetValue(name, out var result)
                ? result
                : UniqueNameResult.Unique);
        }

        public void AddConflict(string name, UniqueNameResult result)
        {
            _results.Add(name, result);
        }
    }
}
