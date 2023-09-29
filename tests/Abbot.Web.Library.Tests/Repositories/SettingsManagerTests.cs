using System;
using System.Linq;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Telemetry;

public class SettingsManagerTests
{
    public class TheGetAllAsyncMethod
    {
        static async Task SetTestSettingsAsync(SettingsManager manager, TestEnvironmentWithData env, Room room)
        {
            await manager.SetAsync(SettingsScope.Global, "global-1", "global-1-value", env.TestData.Abbot.User);
            await manager.SetAsync(SettingsScope.Global, "global-2", "global-2-value", env.TestData.Abbot.User);
            await manager.SetAsync(SettingsScope.Global, "another-one", "another-one-value", env.TestData.Abbot.User);
            await manager.SetAsync(SettingsScope.Organization(env.TestData.Organization), "getalltest:org", "org-value", env.TestData.Abbot.User);
            await manager.SetAsync(SettingsScope.Organization(env.TestData.ForeignOrganization), "foreign-org", "foreign-org-value", env.TestData.Abbot.User);
            await manager.SetAsync(SettingsScope.Member(env.TestData.Member), "member", "member-value", env.TestData.Abbot.User);
            await manager.SetAsync(SettingsScope.Member(env.TestData.ForeignMember), "foreign-member", "foreign-member-value", env.TestData.Abbot.User);
            await manager.SetAsync(SettingsScope.Room(room), "room", "room-value", env.TestData.Abbot.User);

            // Cheat a little and use a negative TTL to set expired settings.
            await manager.SetAsync(SettingsScope.Global, "expired-global", "expired-value", env.TestData.Abbot.User, TimeSpan.FromHours(-2));
            await manager.SetAsync(SettingsScope.Organization(env.TestData.Organization), "getalltest:expired-org-1", "expired-value", env.TestData.Abbot.User, TimeSpan.FromHours(-2));
            await manager.SetAsync(SettingsScope.Organization(env.TestData.ForeignOrganization), "expired-org-2", "expired-value", env.TestData.Abbot.User, TimeSpan.FromHours(-2));
            await manager.SetAsync(SettingsScope.Member(env.TestData.Member), "expired-member-1", "expired-value", env.TestData.Abbot.User, TimeSpan.FromHours(-2));
            await manager.SetAsync(SettingsScope.Member(env.TestData.ForeignMember), "expired-member-2", "expired-value", env.TestData.Abbot.User, TimeSpan.FromHours(-2));
            await manager.SetAsync(SettingsScope.Room(room), "expired-room", "expired-value", env.TestData.Abbot.User, TimeSpan.FromHours(-2));
        }

        [Fact]
        public async Task GetsAllInGlobalScope()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();
            var room = await env.CreateRoomAsync();
            await SetTestSettingsAsync(manager, env, room);

            Assert.Equal(new (string, string, string, int, DateTime, DateTime?)[]
            {
                ("", "global-1", "global-1-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),
                ("", "global-2", "global-2-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),
                ("", "another-one", "another-one-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),

                // Yes, expired settings _are_ returned by GetAllAsync
                ("", "expired-global", "expired-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, env.Clock.UtcNow.AddHours(-2)),
            }, (await manager.GetAllAsync(SettingsScope.Global)).Select(x => (x.Scope, x.Name, x.Value, x.CreatorId, x.Created, x.Expiry)));
        }

        [Fact]
        public async Task GetsAllWithPrefix()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();
            var room = await env.CreateRoomAsync();
            await SetTestSettingsAsync(manager, env, room);

            Assert.Equal(new[]
            {
                ("", "global-1", "global-1-value", env.TestData.Abbot.UserId, env.Clock.UtcNow),
                ("", "global-2", "global-2-value", env.TestData.Abbot.UserId, env.Clock.UtcNow)
            }, (await manager.GetAllAsync(SettingsScope.Global, "global-")).Select(x => (x.Scope, x.Name, x.Value, x.CreatorId, x.Created)));
        }

        [Fact]
        public async Task GetsAllInOrgScope()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();
            var room = await env.CreateRoomAsync();
            await SetTestSettingsAsync(manager, env, room);

            var scope = SettingsScope.Organization(env.TestData.Organization);
            Assert.Equal(new (string, string, string, int, DateTime, DateTime?)[]
            {
                ($"Organization:{env.TestData.Organization.Id}", "getalltest:org", "org-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),
                ($"Organization:{env.TestData.Organization.Id}", "getalltest:expired-org-1", "expired-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, env.Clock.UtcNow.AddHours(-2)),
            }, (await manager.GetAllAsync(scope)).Where(x => x.Name != "AllowAIEnhancements").Select(x => (x.Scope, x.Name, x.Value, x.CreatorId, x.Created, x.Expiry)));
        }

        [Fact]
        public async Task GetsAllInForeignOrgScope()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();
            var room = await env.CreateRoomAsync();
            await SetTestSettingsAsync(manager, env, room);

            var scope = SettingsScope.Organization(env.TestData.ForeignOrganization);
            Assert.Equal(new (string, string, string, int, DateTime, DateTime?)[]
            {
                ($"Organization:{env.TestData.ForeignOrganization.Id}", "foreign-org", "foreign-org-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),
                ($"Organization:{env.TestData.ForeignOrganization.Id}", "expired-org-2", "expired-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, env.Clock.UtcNow.AddHours(-2)),
            }, (await manager.GetAllAsync(scope)).Select(x => (x.Scope, x.Name, x.Value, x.CreatorId, x.Created, x.Expiry)));
        }

        [Fact]
        public async Task GetsAllInMemberScope()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();
            var room = await env.CreateRoomAsync();
            await SetTestSettingsAsync(manager, env, room);

            var scope = SettingsScope.Member(env.TestData.Member);
            Assert.Equal(new (string, string, string, int, DateTime, DateTime?)[]
            {
                ($"Organization:{env.TestData.Organization.Id}/Member:{env.TestData.Member.Id}", "member", "member-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),
                ($"Organization:{env.TestData.Organization.Id}/Member:{env.TestData.Member.Id}", "expired-member-1", "expired-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, env.Clock.UtcNow.AddHours(-2)),
            }, (await manager.GetAllAsync(scope)).Select(x => (x.Scope, x.Name, x.Value, x.CreatorId, x.Created, x.Expiry)));
        }

        [Fact]
        public async Task GetsAllInForeignMemberScope()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();
            var room = await env.CreateRoomAsync();
            await SetTestSettingsAsync(manager, env, room);

            var scope = SettingsScope.Member(env.TestData.ForeignMember);
            Assert.Equal(new (string, string, string, int, DateTime, DateTime?)[]
            {
                ($"Organization:{env.TestData.ForeignOrganization.Id}/Member:{env.TestData.ForeignMember.Id}", "foreign-member", "foreign-member-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),
                ($"Organization:{env.TestData.ForeignOrganization.Id}/Member:{env.TestData.ForeignMember.Id}", "expired-member-2", "expired-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, env.Clock.UtcNow.AddHours(-2)),
            }, (await manager.GetAllAsync(scope)).Select(x => (x.Scope, x.Name, x.Value, x.CreatorId, x.Created, x.Expiry)));
        }

        [Fact]
        public async Task GetsAllInRoomScope()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();
            var room = await env.CreateRoomAsync();
            await SetTestSettingsAsync(manager, env, room);

            var scope = SettingsScope.Room(room);
            Assert.Equal(new (string, string, string, int, DateTime, DateTime?)[]
            {
                ($"Organization:{env.TestData.Organization.Id}/Room:{room.Id}", "room", "room-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, null),
                ($"Organization:{env.TestData.Organization.Id}/Room:{room.Id}", "expired-room", "expired-value", env.TestData.Abbot.UserId, env.Clock.UtcNow, env.Clock.UtcNow.AddHours(-2)),
            }, (await manager.GetAllAsync(scope)).Select(x => (x.Scope, x.Name, x.Value, x.CreatorId, x.Created, x.Expiry)));
        }
    }

    public class TheGetAsyncMethod
    {
        static async Task RunGetAsyncTest(TestEnvironmentWithData env, SettingsScope scope)
        {
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();

            var setting = await manager.GetAsync(scope, "key");
            Assert.Null(setting);

            // Now set it
            await manager.SetAsync(scope, "key", "value", env.TestData.Abbot.User);
            setting = await manager.GetAsync(scope, "key");
            Assert.NotNull(setting);
            Assert.Equal("key", setting.Name);
            Assert.Equal("value", setting.Value);
            Assert.Equal(scope.Name, setting.Scope);
            Assert.Same(env.TestData.Abbot.User, setting.Creator);
            Assert.Equal(env.Clock.UtcNow, setting.Created);
            Assert.Same(env.TestData.Abbot.User, setting.ModifiedBy);
            Assert.Equal(env.Clock.UtcNow, setting.Modified);
        }

        [Fact]
        public async Task ReturnsNullIfValueExpired()
        {
            var env = TestEnvironment.Create();
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();

            await manager.SetAsync(SettingsScope.Global, "temporary-value", "value", env.TestData.Abbot.User, TimeSpan.FromHours(1));

            var setting = await manager.GetAsync(SettingsScope.Global, "temporary-value");
            Assert.Equal("value", setting?.Value);

            env.Clock.AdvanceBy(TimeSpan.FromMinutes(30));
            setting = await manager.GetAsync(SettingsScope.Global, "temporary-value");
            Assert.Equal("value", setting?.Value);

            env.Clock.AdvanceBy(TimeSpan.FromMinutes(40));
            setting = await manager.GetAsync(SettingsScope.Global, "temporary-value");
            Assert.Null(setting);
        }

        [Fact]
        public async Task GetsValuesInGlobalScope()
        {
            var env = TestEnvironment.Create();
            await RunGetAsyncTest(env, SettingsScope.Global);
        }

        [Fact]
        public async Task GetsValuesInOrganizationScope()
        {
            var env = TestEnvironment.Create();
            await RunGetAsyncTest(env, SettingsScope.Organization(env.TestData.Organization));
        }

        [Fact]
        public async Task GetsValuesInMemberScope()
        {
            var env = TestEnvironment.Create();
            await RunGetAsyncTest(env, SettingsScope.Member(env.TestData.Member));
        }

        [Fact]
        public async Task GetsValuesInRoomScope()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await RunGetAsyncTest(env, SettingsScope.Room(room));
        }
    }

    public class TheGetCascadingAsyncMethod
    {
        [Fact]
        public async Task ReturnsMostSpecificScopeInList()
        {
            var env = TestEnvironment.Create();
            var settings = env.Activate<SettingsManager>();

            await settings.SetAsync(SettingsScope.Global, "cascading", "global", env.TestData.User);
            await settings.SetAsync(SettingsScope.Organization(env.TestData.Organization), "cascading", "org", env.TestData.User);

            var result = await settings.GetCascadingAsync("cascading",
                SettingsScope.Member(env.TestData.Member),
                SettingsScope.Organization(env.TestData.Organization),
                SettingsScope.Global);

            Assert.Equal("org", result?.Value);
        }
    }

    public class TheSetAsyncMethod
    {
        static async Task RunSetAsyncTest(TestEnvironmentWithData env, SettingsScope scope)
        {
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();

            await manager.SetAsync(scope, "key", "value", env.TestData.Abbot.User);

            Assert.Equal(new[]
            {
                (scope.Name, "key", "value", env.TestData.Abbot.UserId, env.Clock.UtcNow)
            }, (await env.Db.Settings.ToListAsync()).Select(s => (s.Scope, s.Name, s.Value, s.CreatorId, s.Created)));

            // Now update it
            await manager.SetAsync(scope, "key", "new-value", env.TestData.Abbot.User);

            Assert.Equal(new[]
            {
                (scope.Name, "key", "new-value", env.TestData.Abbot.UserId, env.Clock.UtcNow)
            }, (await env.Db.Settings.ToListAsync()).Select(s => (s.Scope, s.Name, s.Value, s.CreatorId, s.Created)));
        }

        [Fact]
        public async Task SetsValuesInGlobalScope()
        {
            var env = TestEnvironment.Create();
            await RunSetAsyncTest(env, SettingsScope.Global);
        }

        [Fact]
        public async Task SetsValuesInOrganizationScope()
        {
            var env = TestEnvironment.Create();
            await RunSetAsyncTest(env, SettingsScope.Organization(env.TestData.Organization));
        }

        [Fact]
        public async Task SetsValuesInMemberScope()
        {
            var env = TestEnvironment.Create();
            await RunSetAsyncTest(env, SettingsScope.Member(env.TestData.Member));
        }

        [Fact]
        public async Task SetsValuesInRoomScope()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await RunSetAsyncTest(env, SettingsScope.Room(room));
        }
    }

    public class TheSetWithAuditingAsyncMethod
    {
        static async Task RunSetWithAuditingAsyncTest(TestEnvironmentWithData env, SettingsScope scope)
        {
            env.Clock.Freeze();
            var actor = env.TestData.Abbot.User;
            var organization = env.TestData.Organization;
            var manager = env.Activate<SettingsManager>();

            await manager.SetWithAuditingAsync(scope, "key", "value", actor, organization);

            Assert.Equal(new[]
            {
                (scope.Name, "key", "value", env.TestData.Abbot.UserId, env.Clock.UtcNow)
            }, (await env.Db.Settings.ToListAsync()).Select(s => (s.Scope, s.Name, s.Value, s.CreatorId, s.Created)));

            var logEntry = await env.AuditLog.AssertMostRecent<SettingAuditEvent>(
                "Created setting `key` with value `value`.", actor);
            var settingAuditInfo = logEntry.ReadProperties<SettingAuditInfo>();
            Assert.NotNull(settingAuditInfo);
            Assert.Equal(AuditOperation.Created, settingAuditInfo.AuditEventType);
            Assert.Equal(scope.Name, settingAuditInfo.Scope);
            Assert.Equal("key", settingAuditInfo.Name);
            Assert.Equal("value", settingAuditInfo.Value);
            Assert.Null(settingAuditInfo.Expiry);

            // Now update it
            await manager.SetWithAuditingAsync(scope, "key", "new-value", actor, organization);

            Assert.Equal(new[]
            {
                (scope.Name, "key", "new-value", env.TestData.Abbot.UserId, env.Clock.UtcNow)
            }, (await env.Db.Settings.ToListAsync()).Select(s => (s.Scope, s.Name, s.Value, s.CreatorId, s.Created)));

            logEntry = await env.AuditLog.AssertMostRecent<SettingAuditEvent>(
                "Changed setting `key` with value `new-value`.", actor);
            settingAuditInfo = logEntry.ReadProperties<SettingAuditInfo>();
            Assert.NotNull(settingAuditInfo);
            Assert.Equal(AuditOperation.Changed, settingAuditInfo.AuditEventType);
            Assert.Equal(scope.Name, settingAuditInfo.Scope);
            Assert.Equal("key", settingAuditInfo.Name);
            Assert.Equal("new-value", settingAuditInfo.Value);
            Assert.Null(settingAuditInfo.Expiry);
        }

        [Fact]
        public async Task SetsValuesInGlobalScope()
        {
            var env = TestEnvironment.Create();
            await RunSetWithAuditingAsyncTest(env, SettingsScope.Global);
        }

        [Fact]
        public async Task SetsValuesInOrganizationScope()
        {
            var env = TestEnvironment.Create();
            await RunSetWithAuditingAsyncTest(env, SettingsScope.Organization(env.TestData.Organization));
        }

        [Fact]
        public async Task SetsValuesInMemberScope()
        {
            var env = TestEnvironment.Create();
            await RunSetWithAuditingAsyncTest(env, SettingsScope.Member(env.TestData.Member));
        }

        [Fact]
        public async Task SetsValuesInRoomScope()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await RunSetWithAuditingAsyncTest(env, SettingsScope.Room(room));
        }
    }

    public class TheRemoveAsyncMethod
    {
        static async Task RunRemoveAsyncTest(TestEnvironmentWithData env, SettingsScope scope)
        {
            env.Clock.Freeze();
            var manager = env.Activate<SettingsManager>();

            // Remove non-existent value
            await manager.RemoveAsync(scope, "key", env.TestData.Abbot.User);
            Assert.Empty(await env.Db.Settings.ToListAsync());

            // Create a value and then remove it
            await manager.SetAsync(scope, "key", "existing-value", env.TestData.Abbot.User);
            Assert.NotEmpty(await env.Db.Settings.ToListAsync());

            await manager.RemoveAsync(scope, "key", env.TestData.Abbot.User);
            Assert.Empty(await env.Db.Settings.ToListAsync());
        }

        [Fact]
        public async Task RemovesValueInGlobalScope()
        {
            var env = TestEnvironment.Create();
            await RunRemoveAsyncTest(env, SettingsScope.Global);
        }

        [Fact]
        public async Task RemovesValueInOrganizationScope()
        {
            var env = TestEnvironment.Create();
            await RunRemoveAsyncTest(env, SettingsScope.Organization(env.TestData.Organization));
        }

        [Fact]
        public async Task RemovesValueInMemberScope()
        {
            var env = TestEnvironment.Create();
            await RunRemoveAsyncTest(env, SettingsScope.Member(env.TestData.Member));
        }

        [Fact]
        public async Task RemovesValueInRoomScope()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await RunRemoveAsyncTest(env, SettingsScope.Room(room));
        }
    }

    public class TheRemoveWithAuditingAsyncMethod
    {
        static async Task RunRemoveWithAuditingAsyncTest(TestEnvironmentWithData env, SettingsScope scope)
        {
            env.Clock.Freeze();
            var actor = env.TestData.Abbot.User;
            var organization = env.TestData.Organization;
            var manager = env.Activate<SettingsManager>();

            // Remove non-existent value
            await manager.RemoveWithAuditingAsync(scope, "key", actor, organization);
            Assert.Empty(await env.Db.Settings.ToListAsync());
            Assert.Empty(await env.AuditLog.GetRecentActivityAsync(organization));

            // Create a value and then remove it
            await manager.SetAsync(scope, "key", "existing-value", actor);
            Assert.NotEmpty(await env.Db.Settings.ToListAsync());

            await manager.RemoveWithAuditingAsync(scope, "key", actor, organization);
            Assert.Empty(await env.Db.Settings.ToListAsync());

            var logEntry = await env.AuditLog.AssertMostRecent<SettingAuditEvent>(
                "Removed setting `key` with value `existing-value`.", actor);

            var settingAuditInfo = logEntry.ReadProperties<SettingAuditInfo>();
            Assert.NotNull(settingAuditInfo);
            Assert.Equal(AuditOperation.Removed, settingAuditInfo.AuditEventType);
            Assert.Equal(scope.Name, settingAuditInfo.Scope);
            Assert.Equal("key", settingAuditInfo.Name);
            Assert.Equal("existing-value", settingAuditInfo.Value);
            Assert.Null(settingAuditInfo.Expiry);
        }

        [Fact]
        public async Task RemovesValueInGlobalScope()
        {
            var env = TestEnvironment.Create();
            await RunRemoveWithAuditingAsyncTest(env, SettingsScope.Global);
        }

        [Fact]
        public async Task RemovesValueInOrganizationScope()
        {
            var env = TestEnvironment.Create();
            await RunRemoveWithAuditingAsyncTest(env, SettingsScope.Organization(env.TestData.Organization));
        }

        [Fact]
        public async Task RemovesValueInMemberScope()
        {
            var env = TestEnvironment.Create();
            await RunRemoveWithAuditingAsyncTest(env, SettingsScope.Member(env.TestData.Member));
        }

        [Fact]
        public async Task RemovesValueInRoomScope()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await RunRemoveWithAuditingAsyncTest(env, SettingsScope.Room(room));
        }
    }
}
