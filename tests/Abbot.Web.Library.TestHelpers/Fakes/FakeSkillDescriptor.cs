using Serious.Abbot.Metadata;

namespace Serious.TestHelpers
{
    public class FakeSkillDescriptor : ISkillDescriptor
    {
        public FakeSkillDescriptor(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }
    }
}
