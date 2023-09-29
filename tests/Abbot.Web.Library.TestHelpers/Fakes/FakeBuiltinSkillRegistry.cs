using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Metadata;
using Serious.Abbot.Services;
using Serious.Abbot.Skills;

namespace Serious.TestHelpers
{
    public class FakeBuiltinSkillRegistry : IBuiltinSkillRegistry
    {
        readonly Dictionary<string, IBuiltinSkillDescriptor> _skillDescriptors;

        public FakeBuiltinSkillRegistry(IEnumerable<ISkill> skills) : this(skills.ToArray())
        {
        }

        public FakeBuiltinSkillRegistry(params ISkill[] skills)
        {
            _skillDescriptors = ToDescriptors(skills)
                .ToDictionary(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase);
        }

        public void AddSkill(ISkill skill)
        {
            var descriptor = FromSkill(skill);
            _skillDescriptors.Add(descriptor.Name, descriptor);
        }

        public void AddSkills(params ISkill[] skills)
        {
            foreach (var skill in skills)
            {
                AddSkill(skill);
            }
        }

        static IEnumerable<IBuiltinSkillDescriptor> ToDescriptors(IEnumerable<ISkill> skills)
        {
            return skills.Select(FromSkill);
        }

        static IBuiltinSkillDescriptor FromSkill(ISkill skill)
        {
            if (skill is FakeSkill testSkill)
            {
                return new FakeBuiltInSkillDescriptor(testSkill.Name, skill);
            }
            return new BuiltinSkillDescriptor(skill.GetType(), new Lazy<ISkill>(() => skill));
        }

        public IBuiltinSkillDescriptor? this[string name] =>
            _skillDescriptors.TryGetValue(name, out var value)
                ? value
                : null;

        public IReadOnlyList<IBuiltinSkillDescriptor> SkillDescriptors => _skillDescriptors.Values.ToReadOnlyList();
    }
}
