using System;
using System.Threading.Tasks;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;

namespace Serious.TestHelpers
{
    public class FakeRunnerEndpointManager : IRunnerEndpointManager
    {
        public ValueTask<SkillRunnerEndpoint> GetEndpointAsync(Organization organization, CodeLanguage language)
        {
            switch (language)
            {
                case CodeLanguage.CSharp:
                    return new ValueTask<SkillRunnerEndpoint>(new SkillRunnerEndpoint(new("http://localhost:7071/api/skillrunner"), null, true));
                case CodeLanguage.JavaScript:
                    return new ValueTask<SkillRunnerEndpoint>(new SkillRunnerEndpoint(new("http://localhost:7072/api/skillrunner"), null, true));
                case CodeLanguage.Python:
                    return new ValueTask<SkillRunnerEndpoint>(new SkillRunnerEndpoint(new("http://localhost:7073/api/skillrunner"), null, true));
                case CodeLanguage.Ink:
                    return new ValueTask<SkillRunnerEndpoint>(new SkillRunnerEndpoint(new("http://localhost:7074/api/skillrunner"), null, true));
                default:
                    throw new NotImplementedException();
            }
        }

        public Task SetGlobalOverrideAsync(CodeLanguage language, SkillRunnerEndpoint endpoint, Member actor)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint>> GetAppConfigEndpointsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyDictionary<CodeLanguage, SkillRunnerEndpoint>> GetGlobalOverridesAsync()
        {
            throw new NotImplementedException();
        }

        public Task ClearGlobalOverrideAsync(CodeLanguage language, Member actor)
        {
            throw new NotImplementedException();
        }
    }
}
