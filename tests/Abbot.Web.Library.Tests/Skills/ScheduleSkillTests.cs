using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Skills;
using Serious.TestHelpers;

public class ScheduleSkillTests
{
    public class TheOnMessageActivityAsyncMethod
    {
        [Fact]
        public async Task WithNoArgumentsReturnsUsagePattern()
        {
            var env = TestEnvironment.Create();
            var message = FakeMessageContext.Create("schedule", "");
            var skill = env.Activate<ScheduleSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                "`<@U001> schedule {skill}` _enables the skill to receive to be called on a schedule and respond to this channel._",
                message.SingleReply());
        }

        [Fact]
        public async Task WithSkillArgumentThatDoesNotMatchSkillReportsSkillNotFound()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var message = env.CreateFakeMessageContext("schedule", "unknownskill", member);
            var skill = env.Activate<ScheduleSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                "I cannot schedule the nonexistent skill `unknownskill`.",
                message.SingleReply());
        }

        [Fact]
        public async Task CannotAttachWhenInNonAttachableRoom()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug");
            var message = env.CreateFakeMessageContext("schedule", pugSkill.Name, member, room: new Room
            {
                Name = "not-attachable",
                Persistent = false,
                Organization = organization
            });
            var skill = env.Activate<ScheduleSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                "Scheduling a skill can only be done in a channel, not in a Direct Message (DM), the Bot Console, etc.",
                message.SingleReply());
        }

        [Fact]
        public async Task WithDuplicateRoomIdReportsErrorMessage()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug");
            pugSkill.Triggers.Add(new SkillScheduledTrigger
            {
                Name = "The Room",
                CronSchedule = "* * * * *",
                Skill = pugSkill,
                RoomId = "C001",
            });
            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext("schedule", pugSkill.Name, member, room: new Room
            {
                Persistent = true,
                Name = "The Room",
                PlatformRoomId = "C001",
                Organization = organization,
            });
            var skill = env.Activate<ScheduleSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                "The skill `pug` is already scheduled to run every minute, every hour, every day. " +
                "Visit https://app.ab.bot/skills/pug/triggers to see the list of triggers for this skill.",
                message.SingleReply());
        }

        [Fact]
        public async Task RequiresUserIsActiveAndHasAMemberRoleOrHigher()
        {
            var env = TestEnvironment.Create();
            var foreignMember = env.TestData.ForeignMember;
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug");
            var message = env.CreateFakeMessageContext("schedule", pugSkill.Name, from: foreignMember, room: new Room
            {
                Persistent = true,
                Name = "The Room",
                PlatformRoomId = "ConversationId",
                Organization = organization,
            });
            var skill = env.Activate<ScheduleSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "Only users who are members of the organization may schedule a skill. Visit https://app.ab.bot/ to log in and request access.",
                message.SingleReply());
        }

        [Fact]
        public async Task WithAccessDeniedReportsAccessDenied()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug", restricted: true);
            var message = env.CreateFakeMessageContext("schedule",
                $"{pugSkill.Name} A GitHub Webhook",
                member,
                room: new Room
                {
                    Persistent = true,
                    Name = "The Room",
                    PlatformRoomId = "ConversationId",
                    Organization = organization,
                });
            var skill = env.Activate<ScheduleSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Empty(pugSkill.Triggers);
            Assert.Equal(
                "`Use` permission for the skill is required to create a trigger.",
                message.SingleReply());
        }

        [Fact]
        public async Task WithSkillArgumentCreatesHttpTriggerForSkill()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug");
            var message = env.CreateFakeMessageContext("schedule",
                $"{pugSkill.Name} A GitHub Webhook",
                member,
                room: new Room
                {
                    Persistent = true,
                    Name = "room-name",
                    PlatformRoomId = "C00000000",
                    Organization = organization,
                });
            var skill = env.Activate<ScheduleSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            var trigger = pugSkill.Triggers.Single();
            Assert.Equal("room-name", trigger.Name);
            Assert.Equal("C00000000", trigger.RoomId);
            Assert.Equal("A GitHub Webhook", trigger.Description);
            Assert.Equal(
                @"The skill `pug` is now scheduled to respond to the channel <#C00000000>. Visit https://app.ab.bot/skills/pug/triggers to configure the schedule.",
                message.SingleReply());
        }
    }
}
