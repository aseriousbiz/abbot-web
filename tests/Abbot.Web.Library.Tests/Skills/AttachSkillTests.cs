using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Skills;
using Serious.TestHelpers;

public class AttachSkillTests
{
    public class TheOnMessageActivityAsyncMethod
    {
        [Fact]
        public async Task WithNoArgumentsReturnsUsagePattern()
        {
            var env = TestEnvironment.Create();
            var message = env.CreateFakeMessageContext("attach", "");
            var skill = env.Activate<AttachSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                $"`<@{env.TestData.Organization.PlatformBotUserId}> attach {{skill}}` _enables the skill to receive HTTP requests and respond to this channel._",
                message.SingleReply());
        }

        [Fact]
        public async Task WithSkillArgumentThatDoesNotMatchSkillReportsSkillNotFound()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var message = env.CreateFakeMessageContext("attach", "unknownskill", member);
            var skill = env.Activate<AttachSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                "I cannot attach an HTTP trigger to the nonexistent skill `unknownskill`.",
                message.SingleReply());
        }

        [Fact]
        public async Task WithNonAttachableRoomReportsError()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug");
            var message = env.CreateFakeMessageContext("attach", pugSkill.Name, member, room: new Room
            {
                Persistent = false,
                Name = "conversationName",
                Organization = organization
            });
            var skill = env.Activate<AttachSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                "Attaching an HTTP trigger to a skill can only be done in a channel, not in a Direct Message (DM), the Bot Console, etc.",
                message.SingleReply());
        }

        [Fact]
        public async Task WithDuplicateRoomIdReportsErrorMessage()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug");
            pugSkill.Triggers.Add(new SkillHttpTrigger
            {
                Name = "The Room",
                Skill = pugSkill,
                ApiToken = "token",
                RoomId = "room-id",
            });
            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext("attach", pugSkill.Name, member, room: new Room
            {
                Persistent = true,
                PlatformRoomId = "room-id",
                Name = "The Room",
                Organization = organization,
            });
            var skill = env.Activate<AttachSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Contains(
                "An HTTP trigger for `pug` to the channel <#room-id> already exists." +
                " Visit https://app.ab.bot/skills/pug/triggers to see the list of triggers for this skill.",
                message.SingleReply());
        }

        [Fact]
        public async Task RequiresUserIsActiveAndHasAMemberRoleOrHigher()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug");
            var message = env.CreateFakeMessageContext("attach", pugSkill.Name, room: new Room
            {
                Persistent = true,
                Name = "The Room",
                Organization = organization
            });
            var skill = env.Activate<AttachSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "Only users who are members of the organization may attach an HTTP trigger to a skill. Visit https://app.ab.bot/ to log in and request access.",
                message.SingleReply());
        }

        [Fact]
        public async Task ReturnsAccessDeniedMessage()
        {
            var env = TestEnvironment.Create();
            var member = await env.CreateMemberInAgentRoleAsync();
            var organization = env.TestData.Organization;
            var pugSkill = await env.CreateSkillAsync("pug", restricted: true);
            var message = env.CreateFakeMessageContext("attach",
                $"{pugSkill.Name} A GitHub Webhook",
                member,
                room: new Room
                {
                    Persistent = true,
                    Name = "The Room",
                    Organization = organization
                });
            var skill = env.Activate<AttachSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                "`Use` permission for the skill is required to create a trigger.",
                message.SingleReply());
            Assert.Empty(pugSkill.Triggers);
        }

        [Fact]
        public async Task WithSkillArgumentCreatesHttpTriggerForSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var member = await env.CreateMemberInAgentRoleAsync();
            var pugSkill = await env.CreateSkillAsync("pug");
            await env.Permissions.SetPermissionAsync(member, pugSkill, Capability.Admin, member);
            var message = env.CreateFakeMessageContext(
                "attach",
                $"{pugSkill.Name} A GitHub Webhook",
                member,
                room: new Room
                {
                    Persistent = true,
                    Name = "The Room",
                    Organization = organization,
                    PlatformRoomId = "C00000000",
                });
            var skill = env.Activate<AttachSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            var trigger = pugSkill.Triggers.Single();
            Assert.Equal("The Room", trigger.Name);
            Assert.Equal("C00000000", trigger.RoomId);
            Assert.Equal("A GitHub Webhook", trigger.Description);
            Assert.Equal(
                @"The skill `pug` is now attached to the channel <#C00000000>. Visit https://app.ab.bot/skills/pug/triggers to get the secret URL used to call this skill.",
                message.SingleReply());
        }
    }
}
