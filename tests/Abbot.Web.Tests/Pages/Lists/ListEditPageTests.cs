using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Lists;
using Serious.Abbot.Validation;
using Serious.TestHelpers;
using Xunit;

public class ListEditPageTests : PageTestBase<EditPage, ListEditPageTests.TestData>
{
    public new class TestData : PageTestBase<EditPage, ListEditPageTests.TestData>.TestData
    {
        public UserList TestList { get; private set; } = null!;

        protected override async Task SeedAsync(TestEnvironmentWithData env)
        {
            await base.SeedAsync(env);

            TestList = await env.Lists.GetAsync("joke", env.TestData.Organization)
                ?? throw new InvalidOperationException("The joke list should have been created when we created the organization");
        }
    }

    UserList TestList => Env.TestData.TestList;

    public class TheOnGetAsyncMethod : ListEditPageTests
    {
        [Fact]
        public async Task WhenListNotFound_Returns404()
        {
            await InvokePageAsync<NotFoundResult>(p => p.OnGetAsync("blob"));
        }

        [Fact]
        public async Task WhenListFound_RendersPage()
        {
            var (page, _) = await InvokePageAsync<PageResult>(p => p.OnGetAsync("joke"));

            Assert.Equal(TestList.Id, page.Input.Id);
            Assert.Equal(TestList.Name, page.Input.Name);
            Assert.Equal(TestList.Description, page.Input.Description);
        }
    }

    public class TheOnPostAsyncMethod : ListEditPageTests
    {
        [Fact]
        public async Task WhenListNotFound_Returns404()
        {
            await InvokePageAsync<NotFoundResult>(p => p.OnPostAsync("blob"));
        }

        [Fact]
        public async Task WhenRenamedToConflictingName_ReturnsInvalid()
        {
            var skillNameValidator = new FakeSkillNameValidator();
            Builder.ReplaceService<ISkillNameValidator>(skillNameValidator);
            skillNameValidator.AddConflict("chuckle", UniqueNameResult.Conflict(nameof(UserList)));

            using var _ = Env.Db.RaiseIfSaved();

            var (page, result) = await InvokePageAsync<PageResult>(async p => {
                p.Input.Name = "chuckle";

                return await p.OnPostAsync("joke");
            });

            Assert.Collection(page.ModelState,
                pair => {
                    Assert.Equal("Input.Name", pair.Key);
                    Assert.NotNull(pair.Value);
                    Assert.Equal("The list name is not unique.", pair.Value.Errors.Single().ErrorMessage);
                });
        }

        [Fact]
        public async Task WhenValid_SavesChangesAndRedirectsToThePage()
        {
            var (page, redirectResult) = await InvokePageAsync<RedirectToPageResult>(async p => {
                p.Input.Name = "chuckle";
                p.Input.Description = "Some nice chuckles";
                p.Input.Id = 42; // Should _not_ be updated

                return await p.OnPostAsync("joke");
            });

            Assert.Equal(new[] { new KeyValuePair<string, object>("name", "chuckle") },
                redirectResult.RouteValues!);
            Assert.Empty(page.ModelState);
            Assert.Equal("List updated!", page.StatusMessage);

            var actual = await Env.Lists.GetAsync("chuckle", Env.TestData.Organization);
            Assert.NotNull(actual);
            Assert.Equal(TestList.Id, actual.Id);
            Assert.Equal("chuckle", actual.Name);
            Assert.Equal("Some nice chuckles", actual.Description);
        }
    }
}
