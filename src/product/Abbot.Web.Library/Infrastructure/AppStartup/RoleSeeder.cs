using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Security;
using Serious.Logging;

namespace Serious.Abbot.Infrastructure.AppStartup;

/// <summary>
/// Creates the necessary roles in the database. This is primarily needed for new development environments.
/// </summary>
public class RoleSeeder : IRunOnceDataSeeder
{
    static readonly ILogger<RoleSeeder> Log = ApplicationLoggerFactory.CreateLogger<RoleSeeder>();

    readonly IRoleManager _roleManager;
    readonly AbbotContext _db;

    public RoleSeeder(IRoleManager roleManager, AbbotContext db)
    {
        _roleManager = roleManager;
        _db = db;
    }

    public async Task SeedDataAsync()
    {
        // Renames legacy `Member` role to `Agent`. Does nothing if it doesn't exist.
        if (!await RenameRoleAsync("Member", Roles.Agent))
        {
            // We know the role doesn't exist: This won't create it if it already exists.
            await _roleManager.CreateRoleAsync(
                Roles.Agent,
                "Accepted users who may use this site.");
        }

        await _roleManager.CreateRoleAsync(
            Roles.Administrator,
            "Users who may administer their organization.");

        await _roleManager.CreateRoleAsync(
            Roles.Staff,
            "Employees of A Serious Business, Inc.");

        // Fix up all Administrators who are not also Agents.
        var adminsNotAgents = await _db.Members
            .Include(m => m.MemberRoles)
            .ThenInclude(r => r.Role)
            .Where(m => m.MemberRoles.Any(mr => mr.Role.Name == Roles.Administrator))
            .Where(m => m.MemberRoles.All(mr => mr.Role.Name != Roles.Agent))
            .ToListAsync();

        if (adminsNotAgents.Count > 0)
        {
            Log.FixingUpAdministrators(adminsNotAgents.Count);
            var agentRole = await _roleManager.GetRoleAsync(Roles.Agent);
            foreach (var adminNotAgent in adminsNotAgents)
            {
                adminNotAgent.MemberRoles.Add(new MemberRole { Role = agentRole });
                await _db.SaveChangesAsync();
            }
        }

        // Fix up all First Responders who are not also Agents.
        var firstRespondersNotAgents = await _db.Members
            .Include(m => m.MemberRoles)
            .ThenInclude(r => r.Role)
            .Include(m => m.RoomAssignments)
            .Where(m => m.RoomAssignments.Any(ra => ra.Role == RoomRole.FirstResponder))
            .Where(m => m.MemberRoles.All(mr => mr.Role.Name != Roles.Agent))
            .ToListAsync();

        if (firstRespondersNotAgents.Count > 0)
        {
            Log.FixingUpFirstResponders(firstRespondersNotAgents.Count);
            var agentRole = await _roleManager.GetRoleAsync(Roles.Agent);
            foreach (var frNotAgent in firstRespondersNotAgents)
            {
                frNotAgent.MemberRoles.Add(new MemberRole { Role = agentRole });
                await _db.SaveChangesAsync();
            }
        }
    }

    async Task<bool> RenameRoleAsync(string name, string newName)
    {
        var dbRole = await _db.Roles.SingleOrDefaultAsync(r => r.Name == name);
        if (dbRole is null)
        {
            return false;
        }

        dbRole.Name = newName;
        await _db.SaveChangesAsync();
        return true;
    }

    public bool Enabled => true;

    public int Version => 1;
}

static partial class RoleSeederLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Fixing up {AdminCount} Administrators who are not also Agents.")]
    public static partial void FixingUpAdministrators(this ILogger<RoleSeeder> logger, int adminCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Fixing up {FirstRespondersCount} Administrators who are not also Agents.")]
    public static partial void FixingUpFirstResponders(this ILogger<RoleSeeder> logger, int firstRespondersCount);
}
