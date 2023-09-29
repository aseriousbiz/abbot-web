using Serious.Abbot.Entities;
using Serious.TestHelpers;

public class AbbotContextTests
{
    public class TheSaveChangesAsyncMethod
    {
        [Fact]
        public async Task UpdatesCreateDateForEntities()
        {
            var compareDate = DateTimeOffset.UtcNow;
            var db = new FakeAbbotContext();
            var org = new Organization
            {
                Slug = "s123",
                Name = "org",
                Domain = "domain",
                PlatformId = "slack0123",
                PlatformType = PlatformType.Slack
            };
            await db.Organizations.AddAsync(org);
            Assert.Equal(DateTime.MinValue, org.Created);

            await db.SaveChangesAsync();

            Assert.True(org.Created > compareDate);
        }

        [Fact]
        public async Task UpdatesCreatedAndUpdatedDatesForTrackedEntities()
        {
            var db = new FakeAbbotContext();
            var org = new Organization
            {
                Slug = "s123",
                Name = "org",
                Domain = "domain",
                PlatformId = "slack0123",
                PlatformType = PlatformType.Slack
            };
            await db.Organizations.AddAsync(org);
            var member = new Member
            {
                User = new User
                {
                    NameIdentifier = "U123",
                    PlatformUserId = "123",
                    DisplayName = "nick",
                    Email = "test@example.com",
                    Avatar = "avatar"
                },
                Organization = org
            };
            await db.Members.AddAsync(member);
            var skill = new Skill
            {
                Name = "juggling",
                Code = "await Bot.ReplyAsync(\"test\");",
                Language = CodeLanguage.CSharp,
                Creator = member.User,
                ModifiedBy = member.User,
                Organization = org
            };
            await db.Skills.AddAsync(skill);
            await db.Organizations.AddAsync(org);

            await db.SaveChangesAsync();

            Assert.Equal(skill.Created, skill.Modified);

            skill.Code = "/* ... */" + skill.Code;
            await db.SaveChangesAsync();
            Assert.True(skill.Modified > skill.Created);
            Assert.Equal(org.Id, skill.OrganizationId);
        }

        [Fact]
        public async Task DoesNotChangeModifiedDateWhenOnlyCacheKeyChanges()
        {
            var db = new FakeAbbotContext();
            var org = new Organization
            {
                Slug = "s123",
                Name = "org",
                Domain = "domain",
                PlatformId = "slack0123",
                PlatformType = PlatformType.Slack
            };
            await db.Organizations.AddAsync(org);
            var member = new Member
            {
                User = new User
                {
                    NameIdentifier = "U123",
                    PlatformUserId = "123",
                    DisplayName = "nick",
                    Email = "test@example.com",
                    Avatar = "avatar"
                },
                Organization = org
            };
            await db.Members.AddAsync(member);
            var skill = new Skill
            {
                Name = "juggling",
                Code = "await Bot.ReplyAsync(\"test\");",
                Language = CodeLanguage.CSharp,
                Creator = member.User,
                ModifiedBy = member.User,
                Organization = org
            };
            await db.Skills.AddAsync(skill);
            await db.Organizations.AddAsync(org);
            await db.SaveChangesAsync();
            Assert.Equal(skill.Created, skill.Modified);
            skill.CacheKey = "xyz";
            await db.SaveChangesAsync();

            Assert.Equal(skill.Created, skill.Modified);
            Assert.Equal(org.Id, skill.OrganizationId);
        }

        [Fact]
        public async Task SetsDeletedAndRenamesSkill()
        {
            var db = new FakeAbbotContext();
            var org = new Organization
            {
                Slug = "s123",
                Name = "org",
                Domain = "domain",
                PlatformId = "slack0123",
                PlatformType = PlatformType.Slack
            };
            await db.Organizations.AddAsync(org);
            var member = new Member
            {
                User = new User
                {
                    NameIdentifier = "U123",
                    PlatformUserId = "123",
                    DisplayName = "nick",
                    Email = "test@example.com",
                    Avatar = "avatar"
                },
                Organization = org
            };
            await db.Members.AddAsync(member);
            var skill = new Skill
            {
                Name = "juggling",
                Code = "await Bot.ReplyAsync(\"test\");",
                Language = CodeLanguage.CSharp,
                Creator = member.User,
                ModifiedBy = member.User,
                Organization = org
            };
            await db.Skills.AddAsync(skill);
            await db.Organizations.AddAsync(org);
            await db.SaveChangesAsync();

            db.Skills.Remove(skill);
            await db.SaveChangesAsync();


            Assert.True(skill.IsDeleted);
            Assert.True(skill.Name.StartsWith("juggling_DELETED-", StringComparison.Ordinal));
        }
    }
}
