using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Text;

namespace Serious.Abbot.Repositories;

public class PermissionRepository : IPermissionRepository
{
    readonly AbbotContext _db;
    readonly IAuditLog _auditLog;

    public PermissionRepository(AbbotContext db, IAuditLog auditLog)
    {
        _db = db;
        _auditLog = auditLog;
    }

    public async Task<Capability> GetCapabilityAsync(Member member, Skill skill)
    {
        var callerSameOrgAsSkill = member.OrganizationId == skill.OrganizationId;
        if (member.IsInRole(Roles.Administrator) && callerSameOrgAsSkill)
        {
            return Capability.Admin;
        }

        var permission = await GetPermissionAsync(member, skill);

        var capability = permission?.Capability ?? Capability.None;

        if (capability >= Capability.Edit && !callerSameOrgAsSkill)
        {
            // We allow external users to call skills with externally callable patterns/signals.
            // But we NEVER allow external users to edit or admin an external skill. Hence the capability downgrade.
            return Capability.Use;
        }

        return capability;
    }

    public async Task<Capability> SetPermissionAsync(Member member, Skill skill, Capability capability, Member actor)
    {
        if (actor.OrganizationId != skill.OrganizationId)
        {
            throw new InvalidOperationException("Actor must be in the same organization as the skill.");
        }

        if (capability >= Capability.Edit && member.OrganizationId != skill.OrganizationId)
        {
            throw new InvalidOperationException($"Cannot grant {capability} permissions to a member outside the skill's organization.");
        }

        var previousCapability = Capability.None;
        var permission = await GetPermissionAsync(member, skill);
        if (permission is null)
        {
            if (capability == Capability.None)
            {
                // No existing permission. Nothing to do here.
                return Capability.None;
            }

            permission = new Permission
            {
                Member = member,
                Skill = skill,
                Capability = capability,
                Created = DateTimeOffset.UtcNow,
                Creator = actor.User,
            };
            await _db.Permissions.AddAsync(permission);
        }
        else
        {
            previousCapability = permission.Capability;

            if (capability != Capability.None)
            {
                permission.Capability = capability;
            }
            else
            {
                _db.Permissions.Remove(permission);
            }
        }

        await _db.SaveChangesAsync();

        var (eventName, description) = capability == Capability.None
            ? ("Revoked", $"Removed permissions for {member.DisplayName.ToMarkdownInlineCode()} to {skill.Name.ToMarkdownInlineCode()}.")
            : ("Granted", $"Gave {capability.ToVerb().ToMarkdownInlineCode()} permission to {member.DisplayName.ToMarkdownInlineCode()} to {skill.Name.ToMarkdownInlineCode()}.");

        if (previousCapability > capability)
        {
            description += $" (downgraded from {previousCapability.ToString().ToMarkdownInlineCode()})";
        }

        await _auditLog.LogAuditEventAsync(new()
        {
            Type = new("Skill.Permission", eventName),
            Actor = actor,
            Organization = skill.Organization,
            Description = description,
        });

        return previousCapability;
    }

    async Task<Permission?> GetPermissionAsync(Member member, Skill skill)
    {
        if (skill.IsDeleted)
        {
            // This should never happen, so it's basically an assertion.
            throw new InvalidOperationException("Attempting to get permissions for a deleted skill!");
        }
        return await _db.Permissions
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(p => p.MemberId == member.Id && p.SkillId == skill.Id);
    }

    public async Task<IReadOnlyList<Permission>> GetPermissionsForSkillAsync(Skill skill)
    {
        if (skill.IsDeleted)
        {
            // This should never happen, so it's basically an assertion.
            throw new InvalidOperationException("Attempting to get permissions for a deleted skill!");
        }

        return await _db.Permissions
            .IgnoreQueryFilters() // We can ignore the filter because we already checked that skill is not deleted.
            .Include(p => p.Member)
            .ThenInclude(m => m.User)
            .Where(p => p.SkillId == skill.Id)
            .OrderBy(p => p.Capability)
            .ToListAsync();
    }
}
