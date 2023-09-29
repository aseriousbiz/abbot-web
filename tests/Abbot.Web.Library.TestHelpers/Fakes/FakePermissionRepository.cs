using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;

namespace Serious.TestHelpers
{
    public class FakePermissionRepository : IPermissionRepository
    {
        readonly Capability _capabilityOverride;
        readonly List<Permission> _permissions = new();

        public FakePermissionRepository() : this(Capability.None)
        {
        }

        public FakePermissionRepository(Capability capabilityOverride)
        {
            _capabilityOverride = capabilityOverride;
        }

        public void AddPermission(Permission permission)
        {
            _permissions.Add(permission);
        }

        public Task<Capability> GetCapabilityAsync(Member member, Skill skill)
        {
            if (_capabilityOverride != Capability.None)
            {
                return Task.FromResult(_capabilityOverride);
            }

            _ = TryGetPermission(member, skill, out var permission);
            return Task.FromResult(permission.Capability);
        }

        public Task<Capability> SetPermissionAsync(Member member, Skill skill, Capability capability, Member actor)
        {
            bool exists = TryGetPermission(member, skill, out var permission);
            var previous = permission.Capability;
            permission.Capability = capability;
            if (!exists)
            {
                _permissions.Add(permission);
            }

            return Task.FromResult(previous);
        }

        public Task<IReadOnlyList<Permission>> GetPermissionsForSkillAsync(Skill skill)
        {
            return Task.FromResult(_permissions.Where(p => p.SkillId == skill.Id || p.Skill.Id == skill.Id).ToReadOnlyList());
        }

        bool TryGetPermission(Member member, Skill skill, out Permission permission)
        {
            var existing = _permissions.SingleOrDefault(p => p.MemberId == member.Id && p.SkillId == skill.Id);
            if (existing is null)
            {
                permission = new Permission
                {
                    Member = member,
                    MemberId = member.Id,
                    Skill = skill,
                    SkillId = skill.Id,
                    Capability = Capability.None
                };
                return false;
            }

            permission = existing;
            return true;
        }
    }
}
