using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serious.Abbot.Clients;
using Serious.Abbot.Entities;
using Serious.Abbot.Security;
using Serious.Abbot.Telemetry;
using Serious.Text;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Used to manage Abbot roles as well as membership in those roles.
/// </summary>
public class RoleManager : IRoleManager
{
    readonly AbbotContext _db;
    readonly IBackgroundSlackClient _backgroundSlackClient;
    readonly IAuditLog _auditLog;
    readonly IHostEnvironment _hostEnvironment;

    public RoleManager(
        AbbotContext abbotContext,
        IBackgroundSlackClient backgroundSlackClient,
        IAuditLog auditLog,
        IHostEnvironment hostEnvironment)
    {
        _db = abbotContext;
        _backgroundSlackClient = backgroundSlackClient;
        _auditLog = auditLog;
        _hostEnvironment = hostEnvironment;
    }

    public async Task AddUserToRoleAsync(Member subject, string roleName, Member actor, string? staffReason = null)
    {
        // We do not allow active users to take actions, except for Abbot.
        if (!actor.IsAbbot() && !actor.Active)
        {
            throw new InvalidOperationException("Only active users may change a user's roles.");
        }

        var role = await GetRoleAsync(roleName);
        if (role.Name is Roles.Staff)
        {
            if (!_hostEnvironment.IsDevelopment() && !actor.IsStaff())
            {
                throw new InvalidOperationException("Only Staff users may add another user to the Staff role");
            }
        }
        else if (!actor.IsStaff() && !actor.IsAbbot() && !actor.IsAdministrator())
        {
            throw new InvalidOperationException("Only Administrators may manage roles");
        }

        if (staffReason is { Length: > 0 } && !actor.IsStaff())
        {
            throw new InvalidOperationException("Only Staff users may provide a reason for adding a user to a role");
        }

        if (subject.MemberRoles.All(u => u.Role.Name != roleName))
        {
            subject.MemberRoles.Add(new MemberRole
            {
                Role = role,
                Member = subject
            });
            await _db.SaveChangesAsync();
            if (roleName is Roles.Administrator)
            {
                if (actor.IsAbbot())
                {
                    // Every admin is added by another admin except for the first one (aka the installing user).
                    // This is the only admin that Abbot should ever add.
                    _backgroundSlackClient.EnqueueMessageToInstaller(subject.Organization, subject);
                }
                else
                {
                    _backgroundSlackClient.EnqueueAdminWelcomeMessage(subject.Organization, subject, actor);
                }
            }
            await _auditLog.LogAuditEventAsync(new()
            {
                Type = new("Organization.Role", "MemberAdded"),
                Actor = actor,
                Organization = subject.Organization,
                Description = $"Added role {roleName.ToMarkdownInlineCode()} to {FormatUser(subject)}",
                StaffPerformed = staffReason is not null,
                StaffReason = staffReason,
            });
        }
    }

    public async Task RemoveUserFromRoleAsync(Member subject, string roleName, Member actor, string? staffReason = null)
    {
        // We do not allow active users to take actions, except for Abbot.
        if (!actor.IsAbbot() && !actor.Active)
        {
            throw new InvalidOperationException("Only active users may change a user's roles.");
        }

        var role = await GetRoleAsync(roleName);
        if (role.Name is Roles.Staff)
        {
            if (!actor.IsStaff())
            {
                throw new InvalidOperationException("Only Staff users may remove another user from the Staff role");
            }
        }
        else if (!actor.IsAbbot() && !actor.IsAdministrator())
        {
            throw new InvalidOperationException("Only Administrators may manage roles");
        }

        var userRole = subject.MemberRoles.SingleOrDefault(u => u.Role.Name == roleName);
        if (userRole is not null)
        {
            subject.MemberRoles.Remove(userRole);
            await _db.SaveChangesAsync();
            await _auditLog.LogAuditEventAsync(new()
            {
                Type = new("Organization.Role", "MemberRemoved"),
                Actor = actor,
                Organization = subject.Organization,
                Description = $"Removed role {roleName.ToMarkdownInlineCode()} from {FormatUser(subject)}",
                StaffPerformed = staffReason is not null,
                StaffReason = staffReason,
            });
        }
    }

    static string FormatUser(Member subject)
    {
        return $"{subject.DisplayName} (Id: {subject.User.PlatformUserId})".ToMarkdownInlineCode();
    }

    /// <summary>
    /// Syncs the set of roles the member has to the principal.
    /// </summary>
    /// <param name="member">The member of the organization</param>
    /// <param name="principal">The current logged in user</param>
    public void SyncRolesToPrincipal(Member member, ClaimsPrincipal principal)
    {
        var claimedRoles = principal.GetRoleClaimValues().ToList();

        // Clear existing Role claims just in case.
        foreach (var claimedRole in claimedRoles)
        {
            principal.RemoveRoleClaim(claimedRole);
        }

        if (!member.Active)
        {
            return;
        }

        var userRoles = member.MemberRoles.Select(r => r.Role.Name).ToList();

        foreach (var role in userRoles)
        {
            principal.AddRoleClaim(role);
        }
    }

    public async Task SyncRolesFromListAsync(Member member, IReadOnlyCollection<string> roles, Member actor)
    {
        // ToList here is so we're not modifying this collection while iterating.
        var existingRoles = member.MemberRoles.Select(r => r.Role.Name).ToList();
        foreach (var role in roles)
        {
            if (!member.IsInRole(role))
            {
                await AddUserToRoleAsync(member, role, actor);
            }
        }

        foreach (var role in existingRoles.Except(roles))
        {
            await RemoveUserFromRoleAsync(member, role, actor);
        }
    }

    public async Task<Role> CreateRoleAsync(string name, string description)
    {
        var dbRole = await _db.Roles.SingleOrDefaultAsync(r => r.Name == name);
        if (dbRole is null)
        {
            dbRole = new Role
            {
                Name = name,
                Description = description
            };
            await _db.Roles.AddAsync(dbRole);
            await _db.SaveChangesAsync();
        }

        return dbRole;
    }

    public async Task<IReadOnlyList<Member>> GetMembersInRoleAsync(string roleName, Organization organization)
    {
        return await GetMembersInRoleQueryable(roleName, organization)
            .OrderBy(m => m.User.DisplayName)
            .ToListAsync();
    }

    public Task<int> GetCountInRoleAsync(string roleName, Organization organization)
    {
        return GetMembersInRoleQueryable(roleName, organization).CountAsync();
    }

    IQueryable<Member> GetMembersInRoleQueryable(string roleName, Organization organization)
    {
        return _db.Roles
            .Where(r => r.Name == roleName)
            .Include(r => r.MemberRoles)
            .ThenInclude(ur => ur.Member)
            .ThenInclude(m => m.User)
            .Include(r => r.MemberRoles)
            .ThenInclude(ur => ur.Member)
            .ThenInclude(m => m.MemberRoles)
            .ThenInclude(mr => mr.Role)
            .SelectMany(r => r.MemberRoles)
            .Select(ur => ur.Member)
            .Where(u => u.OrganizationId == organization.Id);
    }

    public async Task<IReadOnlyList<Role>> GetRolesAsync()
    {
        return await _db.Roles.ToListAsync();
    }

    public async Task<Role> GetRoleAsync(string name)
    {
        return await _db.Roles.SingleOrDefaultAsync(r => r.Name == name)
            ?? throw new InvalidOperationException($"The role {name} is not in the database.");
    }

    public async Task RestoreMemberAsync(Member subject, Member actor)
    {
        // Call this first to make sure the actor has permission to make this change.
        await AddUserToRoleAsync(subject, Roles.Agent, actor);
        subject.Active = true;
        await _db.SaveChangesAsync();
    }
}
