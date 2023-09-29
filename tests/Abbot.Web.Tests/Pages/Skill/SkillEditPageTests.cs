using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;
using Serious.Abbot.Pages.Skills;
using Serious.AspNetCore.Turbo;
using Xunit;

public class SkillEditPageTests : PageTestBase<EditPage>
{
    public class TheOnGetAsyncMethod : SkillEditPageTests
    {
        [Fact]
        public async Task InitializesInputModelFromSkill()
        {
            var env = Env;
            var skill = await env.CreateSkillAsync("test", codeText: "/* code */", usageText: "how to use this skill", description: "Test Description");

            var (editPage, result) = await InvokePageAsync<PageResult>(p => p.OnGetAsync(skill.Name));

            Assert.Equal(skill.Name, editPage.Skill.Name);
            Assert.Equal(CodeLanguage.CSharp, editPage.Language);
            Assert.Equal("test", editPage.Input.Name);
            Assert.Equal("/* code */", editPage.Input.Code);
            Assert.Equal("how to use this skill", editPage.Input.Usage);
            Assert.Equal("Test Description", editPage.Input.Description);
            Assert.False(editPage.HasSourcePackage);
        }
    }

    public class TheHasSourcePackageProperty : SkillEditPageTests
    {
        [Fact]
        public async Task IsTrueWhenSkillHasSourcePackage()
        {
            var env = Env;
            var skill = await env.CreateSkillAsync("test", codeText: "/* code */", usageText: "how to use this skill", description: "Test Description");
            skill.SourcePackageVersionId = 23;

            var (editPage, _) = await InvokePageAsync<PageResult>(p => p.OnGetAsync(skill.Name));

            Assert.True(editPage.HasSourcePackage);
        }
    }

    public class TheOnPostAsyncMethod : SkillEditPageTests
    {
        [Fact]
        public async Task SavesChangesToExistingSkillAndWritesAssembly()
        {
            var env = Env;
            var skill = await env.CreateSkillAsync("test", codeText: "/* code */", usageText: "how to use this skill", description: "Test Description");

            var (page, _) = await InvokePageAsync<TurboStreamViewResult>(async p => {
                p.Input.Code = "/* New Code */";
                p.Input.Usage = "How not to use this code.";

                return await p.OnPostAsync(skill.Name);
            });

            Assert.Equal(skill.Id, page.Skill.Id);
            Assert.Equal("test", skill.Name);
            Assert.Equal("/* New Code */", skill.Code);
            Assert.Equal("How not to use this code.", skill.UsageText);
            Assert.Equal("Test Description", skill.Description);
        }

        [Fact]
        public async Task DoesNotAllowRestrictingSkillWhenNoPermission()
        {
            var env = Env;
            var skill = await env.CreateSkillAsync("test", codeText: "/* code */", usageText: "how to use this skill", description: "Test Description");

            await InvokePageAsync<TurboStreamViewResult>(async p => {
                p.Input.Restricted = true;

                return await p.OnPostAsync(skill.Name);
            });

            Assert.Equal("test", skill.Name);
            Assert.False(skill.Restricted);
        }

        [Theory]
        [InlineData(PlanType.FoundingCustomer)]
        [InlineData(PlanType.Business)]
        public async Task AllowRestrictingSkillWhenHasPermissionAndPlan(PlanType planType)
        {
            var env = Env;
            var skill = await env.CreateSkillAsync("test", codeText: "/* code */", usageText: "how to use this skill", description: "Test Description", restricted: true);

            await InvokePageAsync<TurboStreamViewResult>(async p => {
                p.Input.Restricted = true;

                return await p.OnPostAsync(skill.Name);
            });

            Assert.Equal("test", skill.Name);
            Assert.True(skill.Restricted);
        }
    }
}
