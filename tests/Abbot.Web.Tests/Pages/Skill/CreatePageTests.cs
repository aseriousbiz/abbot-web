using System.Collections.Generic;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Skills;
using Xunit;

public class CreatePageTests : PageTestBase<CreatePage>
{
    public class TheOnGetAsyncMethod : CreatePageTests
    {
        [Theory]
        [InlineData("csharp", CodeLanguage.CSharp)]
        [InlineData("python", CodeLanguage.Python)]
        [InlineData("javascript", CodeLanguage.JavaScript)]
        [InlineData("unexpected", CodeLanguage.CSharp)]
        public async Task InitializesLanguageInputModel(string language, CodeLanguage expected)
        {
            var (createPage, _) = await InvokePageAsync<PageResult>(p => p.OnGetAsync(language, null));

            Assert.Equal(expected, createPage.Language);
            Assert.Null(createPage.Input.Description);
            Assert.Equal(string.Empty, createPage.Input.Name);
            Assert.Null(createPage.Input.Usage);
            Assert.Equal("Skill", createPage.Input.Type);
        }

        [Fact]
        public async Task WithPackageIdInitializesInputModelForPackage()
        {
            var env = Env;
            var member = env.TestData.Member;
            var skill = await env.CreateSkillAsync("test", language: CodeLanguage.Python);
            var package = new Package
            {
                Language = CodeLanguage.Python,
                Description = "A test package",
                UsageText = "how to use this packaged skill",
                Code = "# packaged code",
                Skill = skill,
                Organization = skill.Organization,
                Versions = new List<PackageVersion>
                {
                    new()
                    {
                        Creator = skill.Creator,
                        CodeCacheKey = "whatevs",
                        MajorVersion = 1,
                        ReleaseNotes = "initial release"
                    }
                }
            };
            await env.Packages.CreateAsync(package, member.User);

            var (createPage, _) = await InvokePageAsync<PageResult>(p => p.OnGetAsync(null, package.Id));

            Assert.NotNull(createPage.PackageBeingInstalled);
            Assert.Equal("how to use this packaged skill", createPage.Input.Usage);
            Assert.Equal(CodeLanguage.Python, createPage.Language);
            Assert.Equal(package.Id, createPage.SourcePackageVersionId);
        }
    }

    public class TheOnPostAsyncMethod : CreatePageTests
    {
        [Theory]
        [InlineData("csharp", CodeLanguage.CSharp, "// Change the code below with the code for your skill! \nawait Bot.ReplyAsync(\"Hello \" + Bot.Arguments);")]
        [InlineData("python", CodeLanguage.Python, "# Change the code below with the code for your skill! \nbot.reply(\"Hello \" + bot.arguments)")]
        [InlineData("javascript", CodeLanguage.JavaScript, "  // Change the code below with the code for your skill! \nawait bot.reply(\"Hello \" + bot.arguments);")]
        public async Task CreatesNewSkill(string language, CodeLanguage expectedLanguage, string expectedCode)
        {
            var env = Env;
            var member = env.TestData.Member;

            var (_, result) = await InvokePageAsync<RedirectToPageResult>(async p => {
                p.Input.Name = "new-skill";
                p.Input.Usage = "How not to use this code.";
                p.Input.Description = "Test Description";

                return await p.OnPostAsync(language, null);
            });

            Assert.NotNull(result?.RouteValues);
            Assert.Equal("new-skill", result.RouteValues["skill"]);
            var created = await env.Skills.GetAsync("new-skill", member.Organization);
            Assert.NotNull(created);
            Assert.Equal(expectedCode, created.Code);
            Assert.Equal("How not to use this code.", created.UsageText);
            Assert.Equal("Test Description", created.Description);
            Assert.Equal(expectedLanguage, created.Language);
        }

        [Fact]
        public async Task WithPackageIdCreatesSkillFromPackage()
        {
            var env = Env;
            var skill = await env.CreateSkillAsync("test", CodeLanguage.Python);
            var member = env.TestData.Member;
            var packageVersion = new PackageVersion
            {
                Creator = skill.Creator,
                CodeCacheKey = "whatevs",
                MajorVersion = 1,
                ReleaseNotes = "initial release"
            };
            var package = new Package
            {
                Language = CodeLanguage.Python,
                Description = "A test package",
                UsageText = "How to use this packaged skill",
                Code = "# packaged code",
                Skill = skill,
                Organization = skill.Organization,
                Versions = new List<PackageVersion>
                {
                    packageVersion
                }
            };
            await env.Packages.CreateAsync(package, member.User);

            var (_, result) = await InvokePageAsync<RedirectToPageResult>(async p => {
                await p.OnGetAsync("javascript", package.Id);

                // User changes name and description
                p.Input.Name = "packaged-skill";
                p.Input.Description = "A really cool skill";

                return await p.OnPostAsync("csharp", package.Id);
            });

            Assert.NotNull(result?.RouteValues);
            Assert.Equal("packaged-skill", result.RouteValues["skill"]);
            var created = await env.Skills.GetAsync("packaged-skill", member.Organization);
            Assert.NotNull(created);
            Assert.Equal("# packaged code", created.Code);
            Assert.Equal("How to use this packaged skill", created.UsageText);
            Assert.Equal("A really cool skill", created.Description);
            Assert.Equal(CodeLanguage.Python, created.Language);
            Assert.Equal(packageVersion.Id, created.SourcePackageVersionId);
        }
    }
}
