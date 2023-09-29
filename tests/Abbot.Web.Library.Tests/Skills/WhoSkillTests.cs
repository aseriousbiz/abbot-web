using Abbot.Common.TestHelpers;
using NetTopologySuite.Geometries;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Skills;
using Serious.TestHelpers.CultureAware;
using Coordinate = Serious.Abbot.Messages.Coordinate;

public class WhoSkillTests
{
    public class WhenAskingAboutUser
    {
        [Theory]
        [InlineData("is George Washington")]
        [InlineData("is Beyonce")]
        [InlineData("ran FC Barcelona in 2020")]
        public async Task UsesDefaultResponderForNonMention(string args)
        {
            var env = TestEnvironment.Create();
            var skill = env.Activate<WhoSkill>();
            var message = env.CreateFakeMessageContext(
                "who",
                args);

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"You want the answer to: who {args}",
                message.SingleReply());
        }

        [Fact]
        public async Task ReportsThatItDoesNotKnowAnythingAboutAPersonInAnotherOrg()
        {
            var env = TestEnvironment.Create();
            var otherOrg = await env.CreateOrganizationAsync();
            var otherMember = await env.CreateMemberAsync(org: otherOrg);
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{otherMember.User.PlatformUserId}>", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { otherMember });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal($"I don’t believe <@{otherMember.User.PlatformUserId}> is even a person in this organization.", reply);
        }

        [Fact]
        public async Task ReportsThatItDoesNotKnowAnythingAboutAPerson()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var member = env.TestData.Member;
            var user = member.User;
            var message = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}>",
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"I don’t know anything about <@{user.PlatformUserId}>. Say `<@{platformBotUserId}> <@{user.PlatformUserId}> is something` to tell " +
                "me something about that human.",
                message.SingleReply());
        }

        [Fact]
        public async Task StripsTrailingPeriodFromLegacyFacts()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            member.Facts.Add(new MemberFact
            {
                Subject = member,
                Content = "a troublemaker.",
                Created = env.Clock.UtcNow,
            });
            member.Facts.Add(new MemberFact
            {
                Subject = member,
                Content = "a skill creator.",
                Created = env.Clock.UtcNow,
            });

            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}>",
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"<@{user.PlatformUserId}> is a skill creator, a troublemaker.",
                message.SingleReply());
        }
    }

    public class WhenAskingAboutSelf
    {
        [Fact]
        public async Task ReportsTheCurrentUserName()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                "am i");
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal(
                $"I thought you would know that by now. You are <@{user.PlatformUserId}>.",
                reply);
        }
    }

    public class WhenAddingInformationAboutUser
    {
        [Fact]
        public async Task CanAddInformationAboutUser()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            user.PlatformUserId = "U8675309";
            await env.Db.SaveChangesAsync();
            var messageContext = env.CreateFakeMessageContext(
                skill: "who",
                args: "is <@U8675309> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal("OK, <@U8675309> is trusted.", reply);
        }

        [Fact]
        public async Task CanNotAddInformationAboutUserInAnotherOrg()
        {
            var env = TestEnvironment.Create();
            var otherOrg = await env.CreateOrganizationAsync();
            var otherMember = await env.CreateMemberAsync(org: otherOrg);
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{otherMember.User.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { otherMember });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal($"I don’t believe <@{otherMember.User.PlatformUserId}> is even a person in this organization.", reply);
        }

        [Fact]
        public async Task CanAddMultipleInformationAboutUser()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);
            await skill.OnMessageActivityAsync(messageContext.WithArguments($"is <@{user.PlatformUserId}> a skill creator"),
                    CancellationToken.None);
            await skill.OnMessageActivityAsync(messageContext.WithArguments($"is <@{user.PlatformUserId}> a troublemaker"),
                CancellationToken.None);
            var lastMessageContext = messageContext.WithArguments($"is <@{user.PlatformUserId}>");
            await skill.OnMessageActivityAsync(lastMessageContext, CancellationToken.None);

            var reply = lastMessageContext.SentMessages.Last();
            Assert.Equal($"<@{user.PlatformUserId}> is a troublemaker, a skill creator, trusted.", reply);
        }

        [Fact]
        public async Task StripsEndingPeriodButNotOtherPunctuationWhenAddingInfoAboutUser()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);
            await skill.OnMessageActivityAsync(messageContext.WithArguments($"is <@{user.PlatformUserId}> a skill creator."),
                CancellationToken.None);
            await skill.OnMessageActivityAsync(messageContext.WithArguments($"is <@{user.PlatformUserId}> a troublemaker?"),
                CancellationToken.None);
            var lastMessageContext = messageContext.WithArguments($"is <@{user.PlatformUserId}>");
            await skill.OnMessageActivityAsync(lastMessageContext, CancellationToken.None);

            var reply = lastMessageContext.SentMessages.Last();
            Assert.Equal($"<@{user.PlatformUserId}> is a troublemaker?, a skill creator, trusted.", reply);
        }

        [Fact]
        public async Task ReportsItAlreadyKnowsSomething()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();
            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(2, messageContext.SentMessages.Count);
            var reply = messageContext.SentMessages.Last();
            Assert.Equal("I know.", reply);
        }

        [Fact]
        public async Task ReportsItAlreadyKnowsSomethingWithAGifForASeriousBizOnly()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            member.Organization.PlatformId = WebConstants.ASeriousBizSlackId;
            await env.Db.SaveChangesAsync();
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();
            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(2, messageContext.SentMessages.Count);
            var reply = messageContext.SentMessages.Last();
            Assert.Equal("https://media.giphy.com/media/s3tpyHuSSr98A/giphy.gif", reply);
        }
    }

    public class WhenRemovingInformationAboutUser
    {
        [Fact]
        public async Task CanForgetInformationAboutUser()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();
            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);
            await skill.OnMessageActivityAsync(messageContext.WithArguments($"is <@{user.PlatformUserId}> a skill creator"),
                CancellationToken.None);
            await skill.OnMessageActivityAsync(messageContext.WithArguments($"is <@{user.PlatformUserId}> a troublemaker"),
                CancellationToken.None);
            var lastMessageContext = messageContext.WithArguments($"is not <@{user.PlatformUserId}> a troublemaker");
            await skill.OnMessageActivityAsync(lastMessageContext, CancellationToken.None);

            var reply = lastMessageContext.SentMessages.Last();
            Assert.Equal($"OK, <@{user.PlatformUserId}> is not a troublemaker.", reply);

            var whoIsMessageContext = messageContext.WithArguments($"is <@{user.PlatformUserId}>");
            await skill.OnMessageActivityAsync(whoIsMessageContext, CancellationToken.None);
            var whoIsReply = whoIsMessageContext.SentMessages.Last();
            Assert.Equal($"<@{user.PlatformUserId}> is a skill creator, trusted.", whoIsReply);
        }

        [Fact]
        public async Task ReportsWhenAbbotKnowsNothingAboutUser()
        {
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var member = env.TestData.Member;
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is not <@{user.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal(
                $"I don’t know anything about <@{user.PlatformUserId}>, so there’s nothing to forget. Say `<@{platformBotUserId}> <@{user.PlatformUserId}> is something` to tell me something about that human.",
                reply);
        }

        [Fact]
        public async Task ReportsWhenAbbotDidNotKnowThatAboutUser()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is <@{user.PlatformUserId}> trusted", // SkillRouter reorders arguments to a standard format.
                mentions: new[] { member });
            var skill = env.Activate<WhoSkill>();
            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var nextMessage = messageContext.WithArguments($"is not <@{user.PlatformUserId}> somebody");
            await skill.OnMessageActivityAsync(nextMessage, CancellationToken.None);

            var reply = nextMessage.SentMessages.Last();
            Assert.Equal("I did not know that.", reply);
        }

        [Fact]
        public async Task ShowsListOfSimilarItemsWhenAbbotDoesNotKnowThatFactAboutUser()
        {
            var env = TestEnvironment.Create();
            var member = env.TestData.Member;
            var user = member.User;
            var skill = env.Activate<WhoSkill>();
            var arguments = new[]
            {   // SkillRouter reorders arguments to a standard format.
                $"is <@{user.PlatformUserId}> a peanut butter afficionado",
                $"is <@{user.PlatformUserId}> a skill creator",
                $"is <@{user.PlatformUserId}> a peanut butter lover",
                $"is <@{user.PlatformUserId}> a troublemaker"
            };
            foreach (var argument in arguments)
            {
                await skill.OnMessageActivityAsync(
                    env.CreateFakeMessageContext(
                        "who",
                        argument,
                        mentions: new[] { member }),
                    CancellationToken.None);
            }
            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"is not <@{user.PlatformUserId}> a peanut",
                mentions: new[] { member });

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal(
                $@"That may be true, but did you mean to say <@{user.PlatformUserId}> is not one of these things?
• `a peanut butter lover`
• `a peanut butter afficionado`".ReplaceLineEndings(),
                reply.ReplaceLineEndings());
        }
    }

    public class WithListVerb
    {
        [Fact]
        [UseCulture("en-US")]
        public async Task ShowsItemsAsList()
        {
            var env = TestEnvironment.Create();
            env.Clock.TravelTo(new DateTime(2023, 4, 7));
            var actorMember = env.TestData.Member;
            var actor = actorMember.User;
            var subjectMember = await env.CreateMemberAsync();
            var subject = subjectMember.User;
            var skill = env.Activate<WhoSkill>();
            var arguments = new[]
            {   // SkillRouter reorders arguments to a standard format.
                $"is <@{subject.PlatformUserId}> a peanut butter afficionado",
                $"is <@{subject.PlatformUserId}> a skill creator",
                $"is <@{subject.PlatformUserId}> a peanut butter lover",
                $"is <@{subject.PlatformUserId}> a troublemaker"
            };
            foreach (var argument in arguments)
            {
                await skill.OnMessageActivityAsync(
                    env.CreateFakeMessageContext(
                        "who",
                        argument,
                        actorMember,
                        new[] { subjectMember }),
                    CancellationToken.None);

                env.Clock.AdvanceBy(TimeSpan.FromMinutes(1));
            }

            var messageContext = env.CreateFakeMessageContext(
                "who",
                $"list <@{subject.PlatformUserId}>",
                actorMember,
                new[] { subjectMember });
            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();

            var expected = $"""
            This is what we know about <@U0004>:
            • `a troublemaker`	(added by {actor.ToMention()} on `Friday, April 7, 2023 12:03:00 AM`)
            • `a peanut butter lover`	(added by {actor.ToMention()} on `Friday, April 7, 2023 12:02:00 AM`)
            • `a skill creator`	(added by {actor.ToMention()} on `Friday, April 7, 2023 12:01:00 AM`)
            • `a peanut butter afficionado`	(added by {actor.ToMention()} on `Friday, April 7, 2023 12:00:00 AM`)
            """;

            Assert.Equal(expected, reply);
        }
    }

    public class TheHelpVerb
    {
        [Theory]
        [InlineData("")]
        [InlineData("help")]
        public async Task ShowsHelpText(string arguments)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var platformBotUserId = organization.PlatformBotUserId;
            var skill = env.Activate<WhoSkill>();
            var messageContext = env.CreateFakeMessageContext(
                "who",
                arguments);

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single().ReplaceLineEndings();
            Assert.Equal($@"`<@{platformBotUserId}> @Username is {{something}}` _remembers {{something}} about the user_.
`<@{platformBotUserId}> @Username is not {{something}}` _forgets {{something}} about the user_.
`<@{platformBotUserId}> who is @UserName` _returns everything I know about the mentioned user_.
`<@{platformBotUserId}> who {{question}}` _tries to answer the question if nobody is mentioned. For example, `@abbot who is George Washington?` or `@abbot who was Marie Curie?`_.
`<@{platformBotUserId}> who is near me` _returns a list of people near you_.
`<@{platformBotUserId}> who is near {{location}}` _returns a list of people near {{location}}_.
`<@{platformBotUserId}> who list @UserName` _returns a list of things I know about the mentioned user and who added those things_.
`<@{platformBotUserId}> who can {{skill}}` _returns a list of people with permissions to {{skill}}_.".ReplaceLineEndings(), reply);
        }
    }

    public class WhenAskingAboutPermissions
    {
        [Fact]
        public async Task CanReportPermissionsOnProtectedSkills()
        {
            var env = TestEnvironment.Create();
            var targetSkill = await env.CreateSkillAsync("pug", restricted: true);
            var actor = await env.CreateAdminMemberAsync();
            var pugAdmins = new[] { "Michelle", "Kelly", "Beyoncé" };
            var pugUsers = new[] { "Justin", "JC", "Lance", "Joey", "Chris" };
            foreach (var admin in pugAdmins)
            {
                var adminMember = await env.CreateMemberAsync($"U00{admin}", admin);
                await env.Permissions.SetPermissionAsync(adminMember, targetSkill, Capability.Admin, actor);
            }
            foreach (var user in pugUsers)
            {
                var userMember = await env.CreateMemberAsync($"U00{user}", user);
                await env.Permissions.SetPermissionAsync(userMember, targetSkill, Capability.Use, actor);
            }
            var messageContext = env.CreateFakeMessageContext(
                "who",
                "can pug", // SkillRouter reorders arguments to a standard format.
                actor);
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal("• `Use  ` - <@U00Justin>, <@U00JC>, <@U00Lance>, <@U00Joey>, <@U00Chris>\n" +
            "• `Admin` - <@U00Michelle>, <@U00Kelly>, <@U00Beyoncé>\nThis list does not include the members of the `Administrator` group who have full permissions to all skills.", reply);
        }

        [Fact]
        public async Task DoesNotReportPermissionsForSkillsInAnotherOrg()
        {
            var env = TestEnvironment.Create();
            var otherOrg = await env.CreateOrganizationAsync();
            var targetSkill = await env.CreateSkillAsync("pug", restricted: true);
            var otherOrgAdmin = await env.CreateAdminMemberAsync(otherOrg);
            var pugAdmins = new[] { "Michelle", "Kelly", "Beyoncé" };
            var pugUsers = new[] { "Justin", "JC", "Lance", "Joey", "Chris" };
            foreach (var admin in pugAdmins)
            {
                var adminMember = await env.CreateMemberAsync($"U00{admin}", admin);
                await env.Permissions.SetPermissionAsync(adminMember, targetSkill, Capability.Admin, env.TestData.Member);
            }
            foreach (var user in pugUsers)
            {
                var userMember = await env.CreateMemberAsync($"U00{user}", user);
                await env.Permissions.SetPermissionAsync(userMember, targetSkill, Capability.Use, env.TestData.Member);
            }
            var messageContext = env.CreateFakeMessageContext(
                "who",
                "can pug", // SkillRouter reorders arguments to a standard format.
                org: otherOrg,
                from: otherOrgAdmin);
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal("pug does not exist.", reply);
        }

        [Fact]
        public async Task ReportsThatASkillHasNoPermissions()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("pug", restricted: true);
            var actor = await env.CreateAdminMemberAsync();
            var messageContext = env.CreateFakeMessageContext(
                "who",
                "can pug", // SkillRouter reorders arguments to a standard format.
                actor);
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal("There are no permissions set on `pug`. Only members of the `Administrator` group " +
                $"have permissions to it. `<@{organization.PlatformBotUserId}> help can` to learn how to give permissions to skills.", reply);
        }

        [Fact]
        public async Task ReportsThatASkillIsUnprotected()
        {
            var env = TestEnvironment.Create();
            await env.CreateSkillAsync("pug", restricted: false);
            var actor = await env.CreateAdminMemberAsync();
            var messageContext = env.CreateFakeMessageContext(
                "who",
                "can pug", // SkillRouter reorders arguments to a standard format.
                actor);
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var reply = messageContext.SentMessages.Single();
            Assert.Equal("`pug` is not restricted so EVERYBODY can!", reply);
        }
    }

    public class WhenAskingWhoIsNearby
    {
        const double CoordinateUnit = 1 / 100.0;

        [Theory]
        [InlineData("is nearby")]
        [InlineData("is near me")]
        [InlineData("is near me?")]
        public async Task WhenLocationIsUnknownReturnsInstructionsForSettingLocation(string args)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("pug", restricted: false);
            var message = env.CreateFakeMessageContext(
                "who",
                args);
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"I do not know your location. Try `<@{organization.PlatformBotUserId}> my location is {{address or zip}}` to tell me your location.",
                message.SingleReply());
        }

        [Fact]
        public async Task ReportsTotalCountWhenReturningSubset()
        {
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var member = env.TestData.Member;
            member.Location = new Point(1 * CoordinateUnit, 2 * CoordinateUnit);

            // We're looking for people within 40 "Coordinate Units" of the member.
            var cheshire = await env.CreateMemberAsync(displayName: "cheshire", location: new Point(1 * CoordinateUnit, 2 * CoordinateUnit));
            var humpty = await env.CreateMemberAsync(displayName: "humpty", location: new Point(10 * CoordinateUnit, 10 * CoordinateUnit));
            var jack = await env.CreateMemberAsync(displayName: "jack", location: new Point(-10 * CoordinateUnit, -10 * CoordinateUnit));
            var jill = await env.CreateMemberAsync(displayName: "jill", location: new Point(-10 * CoordinateUnit, -10 * CoordinateUnit));
            await env.CreateMemberAsync(displayName: "queen", location: new Point(10 * CoordinateUnit, 10 * CoordinateUnit));
            var king = await env.CreateMemberAsync(displayName: "king", location: new Point(10 * CoordinateUnit, 10 * CoordinateUnit));

            await env.Db.SaveChangesAsync();
            await env.CreateSkillAsync("pug", restricted: false);
            var message = env.CreateFakeMessageContext(
                "who",
                "is near me");
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"There are 6 people within 25 miles of you. Here are 5 of them!\n`{cheshire.ToMention()}`, `{humpty.ToMention()}`, `{jack.ToMention()}`, `{jill.ToMention()}`, `{king.ToMention()}`",
                message.SingleReply());
        }

        [Fact]
        public async Task ReportsCountWhenReturningAllUsers()
        {
            var env = TestEnvironmentBuilder.Create()
                .Build();

            var member = env.TestData.Member;
            member.Location = new Point(1 * CoordinateUnit, 2 * CoordinateUnit);

            var cheshire = await env.CreateMemberAsync(displayName: "cheshire", location: new Point(1 * CoordinateUnit, 2 * CoordinateUnit));
            var humpty = await env.CreateMemberAsync(displayName: "humpty", location: new Point(10 * CoordinateUnit, 10 * CoordinateUnit));
            var jack = await env.CreateMemberAsync(displayName: "jack", location: new Point(-10 * CoordinateUnit, -10 * CoordinateUnit));
            await env.CreateMemberAsync(displayName: "nobody", location: new Point(-50 * CoordinateUnit, 1 * CoordinateUnit));

            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext(
                "who",
                "is near me");
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"There are 3 people within 25 miles of you.\n`{cheshire.ToMention()}`, `{humpty.ToMention()}`, `{jack.ToMention()}`",
                message.SingleReply());
        }

        [Fact]
        public async Task ReportsSingleUser()
        {
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var member = env.TestData.Member;
            member.Location = new Point(1 * CoordinateUnit, 2 * CoordinateUnit);
            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext(
                "who",
                "is near me");
            var cheshire = await env.CreateMemberAsync(displayName: "cheshire", location: new Point(1 * CoordinateUnit, 2 * CoordinateUnit));
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"There is one person within 25 miles of you: `{cheshire.ToMention()}`",
                message.SingleReply());
        }

        [Fact]
        public async Task ReportsNobodyIsNearby()
        {
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var member = env.TestData.Member;
            member.Location = new Point(1, 2);
            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext(
                "who",
                "is near me");
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"There is nobody within 25 miles of you. If there are people nearby, they haven’t set their location by saying `<@{platformBotUserId}> my location is _location_` with their location.",
                message.SingleReply());
        }
    }

    public class WhenAskingWhoIsNearLocation
    {
        [Fact]
        public async Task RespondsWhenNoLocationIsSpecified()
        {
            var env = TestEnvironmentBuilder.Create()
                .Build();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            var member = env.TestData.Member;
            member.Location = new Point(1, 2);
            await env.Db.SaveChangesAsync();
            var message = env.CreateFakeMessageContext(
                "who",
                "is near");
            message.FromMember.Location = new Point(1, 2);
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"You’ll have to be more specific. You can ask `<@{platformBotUserId}> who is near me` or `<@{platformBotUserId}> who is near {{location}}` where {{location}} is a zip code or an address.",
                message.SingleReply());
        }

        [Theory]
        [InlineData("is near 98008")]
        [InlineData("is near 98008?")]
        public async Task RespondsWithMessageWhenGeocodingFailsToReturnAddress(string args)
        {
            var env = TestEnvironment.Create();
            var message = env.CreateFakeMessageContext(
                "who",
                args);
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal("Sorry, I could not figure where `98008` is.",
                message.SingleReply());
        }

        [Fact]
        public async Task RespondsWithUsersNearLocation()
        {
            var bellevue = new Point(47.6120759, -122.1112721);
            var kirkland = new Coordinate
            {
                Latitude = 47.6768927,
                Longitude = -122.2059833
            };
            var env = TestEnvironment.Create();
            var bellevueResident = await env.CreateMemberAsync(location: bellevue);
            env.GeocodingApi.AddGeocode("98008", new Geocode
            {
                Coordinate = kirkland
            });
            var message = env.CreateFakeMessageContext(
                "who",
                "is near 98008");
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"There is one person within 25 miles of `98008`: `<@{bellevueResident.User.PlatformUserId}>`",
                message.SingleReply());
        }

        [Fact]
        public async Task RespondsWhenNobodyIsNearLocation()
        {
            var bellevue = new Point(47.6120759, -122.1112721);
            var tacoma = new Coordinate
            {
                Latitude = 47.2528768,
                Longitude = 122.4442906
            };
            var env = TestEnvironment.Create();
            var platformBotUserId = env.TestData.Organization.PlatformBotUserId;
            await env.CreateMemberAsync(location: bellevue);
            env.GeocodingApi.AddGeocode("98008", new Geocode
            {
                Coordinate = tacoma
            });
            var message = env.CreateFakeMessageContext(
                "who",
                "is near 98008");
            var skill = env.Activate<WhoSkill>();

            await skill.OnMessageActivityAsync(message, CancellationToken.None);

            Assert.Equal(
                $"There is nobody within 25 miles of you. If there are people nearby, they haven’t set their location by saying `<@{platformBotUserId}> my location is _location_` with their location.",
                message.SingleReply());
        }
    }
}
