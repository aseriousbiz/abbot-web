using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.TestHelpers.CultureAware;
using Xunit;

public class SkillRepositoryTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task CreatesAUserSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var repository = env.Activate<SkillRepository>();
            var skill = new Skill
            {
                Name = "test-skill",
                Description = "A cool skill",
                Language = CodeLanguage.Python,
                Code = "# comment",
                Organization = organization
            };

            await repository.CreateAsync(skill, user);

            var result = await repository.GetAsync("test-skill", organization);
            Assert.NotNull(result?.Creator);
            Assert.Equal("test-skill", result.Name);
            Assert.Equal("A cool skill", result.Description);
            Assert.Equal(CodeLanguage.Python, result.Language);
            Assert.NotEmpty(result.CacheKey);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization)
                as SkillAuditEvent;
            Assert.NotNull(log);
            Assert.Equal("Created Python skill `test-skill`.", log.Description);
            Assert.Equal(skill.Id, log.EntityId);
            Assert.Equal(skill.Id, log.SkillId);
            Assert.Equal(skill.Name, log.SkillName);
            Assert.Equal(CodeLanguage.Python, log.Language);
        }

        [Fact]
        public async Task CreatesAUserSkillFromPackage()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var user = env.TestData.User;
            var repository = env.Activate<SkillRepository>();
            var skill = new Skill
            {
                Name = "test-skill",
                Description = "A cool skill",
                Language = CodeLanguage.Python,
                Code = "# comment",
                Organization = organization,
                SourcePackageVersionId = 123
            };

            await repository.CreateAsync(skill, user);

            var result = await repository.GetAsync("test-skill", organization);
            Assert.NotNull(result?.Creator);
            Assert.Equal("test-skill", result.Name);
            Assert.Equal("A cool skill", result.Description);
            Assert.Equal(CodeLanguage.Python, result.Language);
            Assert.NotEmpty(result.CacheKey);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization)
                as SkillAuditEvent;
            Assert.NotNull(log);
            Assert.Equal("Created Python skill `test-skill` from package.", log.Description);
            Assert.Equal(skill.Id, log.EntityId);
            Assert.Equal(skill.Id, log.SkillId);
            Assert.Equal(skill.Name, log.SkillName);
            Assert.Equal(CodeLanguage.Python, log.Language);
        }
    }

    public class TheUpdateAsyncMethod
    {
        [Fact]
        public async Task UpdatesSkillAndCreatesVersionSnapshot()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            var modifier = (await env.CreateMemberAsync()).User;
            var lastModifier = (await env.CreateMemberAsync()).User;
            var repository = env.Activate<SkillRepository>();
            var skill = new Skill
            {
                Name = "test-skill",
                Description = "A cool skill",
                Language = CodeLanguage.CSharp,
                Code = "/* comment */",
                Organization = organization
            };
            await repository.CreateAsync(skill, creator);
            var result = await repository.GetAsync("test-skill", organization);
            Assert.NotNull(result?.Creator);

            await repository.UpdateAsync(result, new SkillUpdateModel
            {
                UsageText = "New Usage",
                Name = "nest-skill"
            }, modifier);

            Assert.Same(modifier, result.ModifiedBy);
            Assert.Equal("New Usage", result.UsageText);
            Assert.Single(result.Versions);
            var version = result.Versions.Single();
            Assert.Equal("", version.UsageText);
            Assert.Equal("test-skill", version.Name);
            Assert.Null(version.Code);
            Assert.Null(version.Description);
            Assert.Same(result, version.Skill);
            Assert.Same(creator, version.Creator);

            await repository.UpdateAsync(result, new SkillUpdateModel { Code = "/* Comments */" }, lastModifier);
            Assert.Same(lastModifier, result.ModifiedBy);
            var latestVersion = result.Versions.OrderByDescending(v => v.Created).First();
            Assert.Equal("/* comment */", latestVersion.Code);
            Assert.Same(modifier, latestVersion.Creator);
            var nextLog = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(nextLog);
            Assert.StartsWith("Edited C# Code of skill `nest-skill`.", nextLog.Description);
        }

        [Fact]
        [UseCulture("en-US")]
        public async Task LogsChangeOfRestrictedStatusSeparatelyFromOtherChanges()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            var modifier = (await env.CreateMemberAsync()).User;
            var repository = env.Activate<SkillRepository>();
            var skill = new Skill
            {
                Name = "test-skill",
                Description = "A cool skill",
                Language = CodeLanguage.CSharp,
                Code = "/* comment */",
                Organization = organization
            };
            await repository.CreateAsync(skill, creator);
            var result = await repository.GetAsync("test-skill", organization);
            Assert.NotNull(result?.Creator);

            await repository.UpdateAsync(result, new SkillUpdateModel
            {
                UsageText = "new usage",
                Description = "New Description",
                Name = "test2",
                Restricted = true
            }, modifier);

            var entries = await env.Db.AuditEvents.OrderByDescending(e => e.Id).ToListAsync();
            Assert.Equal("Changed properties `Description` and `Usage` of skill `test2`.", entries[0].Description);
            Assert.Equal("Restricted skill `test2`.", entries[1].Description);
            Assert.Equal("Renamed skill `test-skill` to `test2`.", entries[2].Description);
        }

        [Fact]
        [UseCulture("en-US")]
        public async Task LogsChangeOfEnabledSeparatelyFromOtherChanges()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            var modifier = (await env.CreateMemberAsync()).User;
            var repository = env.Activate<SkillRepository>();
            var skill = new Skill
            {
                Name = "test-skill",
                Description = "A cool skill",
                Language = CodeLanguage.CSharp,
                Code = "/* comment */",
                Organization = organization,
                Enabled = false
            };
            await repository.CreateAsync(skill, creator);
            var result = await repository.GetAsync("test-skill", organization);
            Assert.NotNull(result?.Creator);

            await repository.UpdateAsync(result, new SkillUpdateModel
            {
                UsageText = "new usage",
                Description = "New Description",
                Enabled = true
            }, modifier);

            var entries = await env.Db.AuditEvents.OrderByDescending(e => e.Id).ToListAsync();
            Assert.Equal("Changed properties `Description` and `Usage` of skill `test-skill`.", entries[0].Description);
            Assert.Equal("Enabled C# skill `test-skill`.", entries[1].Description);
        }

        [Fact]
        public async Task OnlyLogsEnableDisableIfOnlyEnabledChanged()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            var modifier = (await env.CreateMemberAsync()).User;
            var repository = env.Activate<SkillRepository>();
            var skill = new Skill
            {
                Name = "test-skill",
                Description = "A cool skill",
                Language = CodeLanguage.CSharp,
                Code = "/* comment */",
                Organization = organization,
                Enabled = true
            };
            await repository.CreateAsync(skill, creator);
            var result = await repository.GetAsync("test-skill", organization);
            Assert.NotNull(result?.Creator);

            await repository.UpdateAsync(result, new SkillUpdateModel
            {
                Enabled = false
            }, modifier);

            var entries = await env.Db.AuditEvents.OrderByDescending(e => e.Id).ToListAsync();
            Assert.Equal("Disabled C# skill `test-skill`.", entries[0].Description);
        }

        [Fact]
        public async Task LogsScopeChanged()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            var modifier = (await env.CreateMemberAsync()).User;
            var repository = env.Activate<SkillRepository>();
            var skill = new Skill
            {
                Name = "test-skill",
                Description = "A cool skill",
                Language = CodeLanguage.CSharp,
                Code = "/* comment */",
                Organization = organization,
                Enabled = true
            };
            await repository.CreateAsync(skill, creator);
            var result = await repository.GetAsync("test-skill", organization);
            Assert.NotNull(result?.Creator);

            await repository.UpdateAsync(result, new SkillUpdateModel
            {
                Scope = SkillDataScope.Conversation
            }, modifier);

            var entries = await env.Db.AuditEvents.OrderByDescending(e => e.Id).ToListAsync();
            Assert.Equal("Changed property `Scope` of skill `test-skill`.", entries[0].Description);
        }

    }

    public class TheRemoveAsyncMethod
    {
        [Fact]
        public async Task RemovesAUserSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill", CodeLanguage.JavaScript);
            var repository = env.Activate<SkillRepository>();

            await repository.RemoveAsync(skill, creator);

            var result = await repository.GetAsync("test-skill", organization);
            Assert.Null(result?.Creator);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Removed JavaScript skill `test-skill`.", log.Description);
        }
    }

    public class TheToggleEnabledAsyncMethod
    {
        [Fact]
        public async Task ChangesEnabledStateAndLogsIt()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill", CodeLanguage.Python);
            var repository = env.Activate<SkillRepository>();

            await repository.ToggleEnabledAsync(skill, false, creator);

            var result = await repository.GetAsync("test-skill", organization);
            Assert.False(result?.Enabled);
            var log = await env.AuditLog.GetMostRecentLogEntry(organization);
            Assert.NotNull(log);
            Assert.Equal("Disabled Python skill `test-skill`.", log.Description);
        }
    }

    public class TheGetAllAsyncMethod
    {
        [Fact]
        public async Task IncludesCreator()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var creator = env.TestData.User;
            await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillRepository>();

            var skills = await repository.GetAllAsync(organization);

            Assert.Equal(1, skills.Count);
            Assert.Equal(creator.Id, skills[0].Creator.Id);
        }
    }

    public class TheGetDataAsyncMethod
    {
        [Fact]
        public async Task RetrieveDataByKey()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("test-skill");
            skill.Data.Add(new SkillData { Key = "KEY", Value = "value" });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<SkillRepository>();

            var data = await repository.GetDataAsync(skill, "key");

            Assert.NotNull(data);
            Assert.Equal("value", data.Value);
        }

        [Fact]
        public async Task RetrievesNullForMismatchKey()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillRepository>();

            var data = await repository.GetDataAsync(skill, "key");

            Assert.Null(data);
        }

        [Theory]
        [InlineData("the-key/can-be/anything", SkillDataScope.Conversation, "42", "42")]
        [InlineData("the-key/can-be/anything", SkillDataScope.Room, "42", "42")]
        [InlineData("the-key/can-be/anything", SkillDataScope.User, "42", "42")]
        public async Task RetrieveScopedDataByKey(string key, SkillDataScope scope, string dataContextId, string requestedContextId)
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("test-skill");
            skill.Data.Add(new SkillData
            {
                Key = key.ToUpperInvariant(),
                Value = "value",
                Scope = scope,
                ContextId = dataContextId
            });
            await env.Db.SaveChangesAsync();
            var repository = env.Activate<SkillRepository>();

            var data = await repository.GetDataAsync(skill, key, scope, requestedContextId);

            Assert.NotNull(data);
            Assert.Equal("value", data.Value);
        }

        [Theory]
        [InlineData("the-key/can-be/anything", SkillDataScope.Conversation, "42", SkillDataScope.Room, "42")]
        [InlineData("the-key/can-be/anything", SkillDataScope.Conversation, "42", SkillDataScope.Conversation, "4")]
        [InlineData("the-key/can-be/anything", SkillDataScope.User, "42", SkillDataScope.User, "4")]
        public async Task ReturnsNullForMismatchedScopedOrContextId(string key, SkillDataScope dataScope, string dataContextId, SkillDataScope queriedScope, string queriedContextId)
        {
            var env = TestEnvironment.Create();
            var user = env.TestData.User;
            var skill = await env.CreateSkillAsync("test-skill");
            skill.Data.Add(new SkillData
            {
                Key = key.ToUpperInvariant(),
                Value = "value",
                Scope = dataScope,
                ContextId = dataContextId
            });
            await env.Db.SaveChangesAsync();
            user.Id = int.Parse(dataContextId);
            var repository = env.Activate<SkillRepository>();

            var data = await repository.GetDataAsync(skill, key, queriedScope, queriedContextId);

            Assert.Null(data);
        }
    }

    public class TheAddExemplarAsyncMethod
    {
        [Fact]
        public async Task AddsAnExemplarToTheSkill()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillRepository>();

            var result = await repository.AddExemplarAsync(skill,
                "The Exemplar",
                new()
                {
                    Arguments = "Args",
                    EmbeddingVector = new[] { 1.0, 2.0 }
                },
                env.TestData.Member);
            Assert.Equal(EntityResultType.Success, result.Type);

            await env.ReloadAsync(skill);
            var exemplar = Assert.Single(skill.Exemplars);
            Assert.Equal("The Exemplar", exemplar.Exemplar);
            Assert.Equal("Args", exemplar.Properties.Arguments);
            Assert.Equal(new[] { 1.0, 2.0 }, exemplar.Properties.EmbeddingVector);

            var log = await env.AuditLog.AssertMostRecent<AuditEvent>(
                "Created an exemplar for the `test-skill` skill.",
                organization: skill.Organization);
            Assert.Equal(new("Skill.Exemplar", AuditOperation.Created), log.Type);
            Assert.Equal(result.Entity.Require().Id, log.EntityId);
            Assert.Equal(new (string, object)[]
            {
                ("Arguments", "Args"),
                ("Text", "The Exemplar"),
            }, log.ReadPropertiesAsTuples());
        }
    }

    public class TheUpdateExemplarAsyncMethod
    {
        [Fact]
        public async Task UpdatesAnExistingExemplar()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillRepository>();

            var result = await repository.AddExemplarAsync(skill,
                "The Exemplar",
                new()
                {
                    Arguments = "Args",
                    EmbeddingVector = new[] { 1.0, 2.0 }
                },
                env.TestData.Member);
            Assert.Equal(EntityResultType.Success, result.Type);

            await repository.UpdateExemplarAsync(result.Entity.Require(),
                "Updated Text",
                new()
                {
                    Arguments = "Updated Args",
                    EmbeddingVector = new[] { 3.0, 4.0 }
                },
                env.TestData.Member);

            await env.ReloadAsync(skill);
            var exemplar = Assert.Single(skill.Exemplars);
            Assert.Equal("Updated Text", exemplar.Exemplar);
            Assert.Equal("Updated Args", exemplar.Properties.Arguments);
            Assert.Equal(new[] { 3.0, 4.0 }, exemplar.Properties.EmbeddingVector);

            var log = await env.AuditLog.AssertMostRecent<AuditEvent>(
                "Updated an exemplar for the `test-skill` skill.",
                organization: skill.Organization);
            Assert.Equal(new("Skill.Exemplar", AuditOperation.Changed), log.Type);
            Assert.Equal(exemplar.Id, log.EntityId);
            Assert.Equal(new (string, object)[]
            {
                ("Arguments", "Updated Args"),
                ("Text", "Updated Text"),
            }, log.ReadPropertiesAsTuples());
        }
    }

    public class TheRemoveExemplarAsyncMethod
    {
        [Fact]
        public async Task RemovesAnExistingExemplar()
        {
            var env = TestEnvironment.Create();
            var skill = await env.CreateSkillAsync("test-skill");
            var repository = env.Activate<SkillRepository>();

            var result = await repository.AddExemplarAsync(skill,
                "The Exemplar",
                new()
                {
                    Arguments = "Args",
                    EmbeddingVector = new[] { 1.0, 2.0 }
                },
                env.TestData.Member);
            Assert.Equal(EntityResultType.Success, result.Type);

            await repository.RemoveExemplarAsync(result.Entity.Require(),
                env.TestData.Member);

            await env.ReloadAsync(skill);
            Assert.Empty(skill.Exemplars);
            Assert.Empty(env.Db.SkillExemplars);

            var log = await env.AuditLog.AssertMostRecent<AuditEvent>(
                "Removed an exemplar for the `test-skill` skill.",
                organization: skill.Organization);
            Assert.Equal(new("Skill.Exemplar", AuditOperation.Removed), log.Type);
            Assert.Equal(result.Entity.Id, log.EntityId);
            Assert.Equal(new (string, object)[]
            {
                ("Arguments", "Args"),
                ("Text", "The Exemplar"),
            }, log.ReadPropertiesAsTuples());
        }
    }
}
