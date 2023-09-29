using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;

namespace Serious.TestHelpers
{
    public class FakeSkillAssemblyIdentifier : ICompiledSkillIdentifier
    {
        public string PlatformId { get; set; } = string.Empty;
        public PlatformType PlatformType { get; set; }
        public int SkillId { get; set; }
        public string SkillName { get; set; } = string.Empty;
        public string CacheKey { get; set; } = string.Empty;
        public CodeLanguage Language { get; set; }
    }
}
