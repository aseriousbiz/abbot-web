using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Security;
using Serious.EntityFrameworkCore;
using Xunit;

public class RoleSeederTests
{
    public class TheSeedDataAsyncMethod
    {
        [Fact]
        public async Task CreatesTheNecessaryRoles()
        {
            var env = TestEnvironment.CreateWithoutData();
            var roles = await env.Roles.GetRolesAsync();
            Assert.Empty(roles);
            var roleSeeder = env.Activate<RoleSeeder>();

            await roleSeeder.SeedDataAsync();

            roles = await env.Roles.GetRolesAsync();
            Assert.Collection(roles,
                r => Assert.Equal(Roles.Agent, r.Name),
                r => Assert.Equal(Roles.Administrator, r.Name),
                r => Assert.Equal(Roles.Staff, r.Name));
        }

        [Fact]
        public async Task AddsAgentRoleToAdministratorsWithoutRole()
        {
            var env = TestEnvironment.Create();
            var agentRole = await env.Roles.GetRoleAsync(Roles.Agent);
            var adminRole = await env.Roles.GetRoleAsync(Roles.Administrator);
            Assert.NotNull(adminRole);
            var adminNoAgent1 = await env.CreateMemberAsync();
            var adminNoAgent2 = await env.CreateMemberAsync();
            var adminAndAgent = await env.CreateMemberAsync();
            var onlyAgent = await env.CreateMemberAsync();
            var noRoles = await env.CreateMemberAsync();
            adminNoAgent1.MemberRoles.Add(new MemberRole { Role = adminRole });
            adminNoAgent2.MemberRoles.Add(new MemberRole { Role = adminRole });
            adminAndAgent.MemberRoles.Add(new MemberRole { Role = agentRole });
            adminAndAgent.MemberRoles.Add(new MemberRole { Role = adminRole });
            onlyAgent.MemberRoles.Add(new MemberRole { Role = agentRole });
            await env.Db.SaveChangesAsync();
            var roleSeeder = env.Activate<RoleSeeder>();

            await roleSeeder.SeedDataAsync();

            await env.ReloadAsync(adminNoAgent1, adminNoAgent2, adminAndAgent, onlyAgent, noRoles);
            Assert.True(adminNoAgent1.IsAgent());
            Assert.True(adminNoAgent2.IsAgent());

            Assert.True(adminNoAgent1.IsAdministrator());
            Assert.True(adminNoAgent2.IsAdministrator());

            Assert.True(adminAndAgent.IsAgent());
            Assert.True(adminAndAgent.IsAdministrator());

            Assert.True(onlyAgent.IsAgent());
            Assert.False(onlyAgent.IsAdministrator());

            Assert.False(noRoles.IsAgent());
            Assert.False(noRoles.IsAdministrator());
        }

        [Fact]
        public async Task AddsAgentRoleToFirstRespondersWithoutRole()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var agentRole = await env.Roles.GetRoleAsync(Roles.Agent);
            var adminRole = await env.Roles.GetRoleAsync(Roles.Administrator);
            Assert.NotNull(adminRole);
            var frNoAgent1 = await env.CreateMemberAsync();
            var frNoAgent2 = await env.CreateMemberAsync();
            var frAndAgent = await env.CreateMemberAsync();
            var onlyAgent = await env.CreateMemberAsync();
            var noRoles = await env.CreateMemberAsync();
            // AssignMemberAsync won't do anything if the member is not an agent, so we do this manually.
            frNoAgent1.RoomAssignments.Add(
                new()
                {
                    Room = room,
                    Role = RoomRole.FirstResponder,
                    Creator = env.TestData.User
                }
            );
            frNoAgent2.RoomAssignments.Add(
                new()
                {
                    Room = room,
                    Role = RoomRole.FirstResponder,
                    Creator = env.TestData.User
                }
            );
            frAndAgent.RoomAssignments.Add(
                new()
                {
                    Room = room,
                    Role = RoomRole.FirstResponder,
                    Creator = env.TestData.User
                }
            );
            frAndAgent.MemberRoles.Add(new MemberRole { Role = agentRole });
            onlyAgent.MemberRoles.Add(new MemberRole { Role = agentRole });
            await env.Db.SaveChangesAsync();
            var roleSeeder = env.Activate<RoleSeeder>();

            await roleSeeder.SeedDataAsync();

            await env.ReloadAsync(frNoAgent1, frNoAgent2, frAndAgent, onlyAgent, noRoles);
            Assert.True(frNoAgent1.IsAgent());
            Assert.True(frNoAgent2.IsAgent());

            Assert.True(room.GetFirstResponders().Any(r => r.Id == frNoAgent1.Id));
            Assert.True(room.GetFirstResponders().Any(r => r.Id == frNoAgent2.Id));

            Assert.True(frAndAgent.IsAgent());
            Assert.True(room.GetFirstResponders().Any(r => r.Id == frAndAgent.Id));

            Assert.True(onlyAgent.IsAgent());
            Assert.False(room.GetFirstResponders().Any(r => r.Id == onlyAgent.Id));

            Assert.False(noRoles.IsAgent());
            Assert.False(room.GetFirstResponders().Any(r => r.Id == noRoles.Id));
        }
    }
}
