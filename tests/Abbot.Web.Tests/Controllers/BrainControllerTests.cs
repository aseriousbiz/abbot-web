using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Controllers;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.TestHelpers;
using Xunit;

public class BrainControllerTests
{
    public class TheGetAsyncMethodWithKey : ControllerTestBase<BrainController>
    {
        [Fact]
        public async Task ReturnsNotFoundIfDataNotFound()
        {
            AuthenticateAs(Env.TestData.Member, new(404));

            var (_, result) = await InvokeControllerAsync(c => c.GetAsync("key"));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsNotFoundIfDataNotFoundForOrganization()
        {
            var skill = await Env.CreateSkillAsync("test");
            skill.Data.Add(new SkillData
            {
                Key = "key",
                Value = "the value",
                Creator = Env.TestData.User
            });
            await Env.Db.SaveChangesAsync();
            AuthenticateAs(Env.TestData.Member, new(404));

            var (_, result) = await InvokeControllerAsync(c => c.GetAsync("key"));

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ReturnsResultWithData()
        {
            var skill = await Env.CreateSkillAsync("test");
            skill.Data.Add(new SkillData
            {
                Key = "KEY",
                Value = "the value",
                Creator = Env.TestData.User
            });
            await Env.Db.SaveChangesAsync();
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAsync("key"));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillDataResponse>(objectResult.Value);
            Assert.NotNull(response);
            Assert.Equal("KEY", response.Key);
            Assert.Equal("the value", response.Value);
        }
    }

    public class TheGetAsyncMethodWithoutKey : ControllerTestBase<BrainController>
    {
        [Fact]
        public async Task ReturnsAllDataForSkill()
        {
            var skill = await Env.CreateSkillAsync("test");
            skill.Data.Add(new SkillData
            {
                Key = "key1",
                Value = "the value1",
                Creator = Env.TestData.User
            });
            skill.Data.Add(new SkillData
            {
                Key = "key2",
                Value = "the value2",
                Creator = Env.TestData.User
            });
            await Env.Db.SaveChangesAsync();
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.GetAsync(null));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var data = Assert.IsType<Dictionary<string, string>>(objectResult.Value);
            Assert.NotNull(data);
            Assert.Equal(new KeyValuePair<string, string>[]
            {
                 new ("key1", "the value1"),
                 new ("key2", "the value2"),
            }, data.OrderBy(k => k.Key).ToArray());
        }
    }

    public class ThePostAsyncMethod : ControllerTestBase<BrainController>
    {
        [Fact]
        public async Task UpdatesExistingData()
        {
            var skill = await Env.CreateSkillAsync("test");
            skill.Data.Add(new SkillData
            {
                Key = "KEY",
                Value = "THE VALUE",
                Creator = Env.TestData.User
            });
            await Env.Db.SaveChangesAsync();
            var request = new SkillDataUpdateRequest
            {
                Value = "new value"
            };
            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.PostAsync("key", request));

            var objectResult = Assert.IsType<ObjectResult>(result);
            var response = Assert.IsType<SkillDataResponse>(objectResult.Value);
            Assert.NotNull(response);
            Assert.Equal("KEY", response.Key);
            Assert.Equal("new value", response.Value);
            var retrieved = await Env.Skills.GetDataAsync(skill, "key");
            Assert.NotNull(retrieved);
            Assert.Equal("new value", retrieved.Value);
        }
    }

    public class TheDeleteAsyncMethod : ControllerTestBase<BrainController>
    {
        [Fact]
        public async Task DeletesData()
        {
            var skill = await Env.CreateSkillAsync("test");
            skill.Data.Add(new SkillData
            {
                Key = "KEY",
                Value = "THE VALUE",
                Creator = Env.TestData.User
            });
            await Env.Db.SaveChangesAsync();

            AuthenticateAs(Env.TestData.Member, skill);

            var (_, result) = await InvokeControllerAsync(c => c.DeleteAsync("key"));

            Assert.IsType<OkResult>(result);
            var retrieved = await Env.Skills.GetDataAsync(skill, "key");
            Assert.Null(retrieved);
        }
    }
}
