using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;

public class TagRepositoryTests
{
    public class TheGetTagsByNameAsyncMethod
    {
        [Fact]
        public async Task GetsAllTagsByNames()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<TagRepository>();
            var existingTag0 = await repository.CreateAsync(
                new Tag { Name = "tAg0", Organization = env.TestData.Organization },
                env.TestData.User);
            var existingTag1 = await repository.CreateAsync(
                new Tag { Name = "Tag1", Organization = env.TestData.Organization },
                env.TestData.User);

            var tags = await repository.GetTagsByNamesAsync(new[] { "TAG0", "tag1", "tag2" }, env.TestData.Organization);

            Assert.True(tags[0].IsSuccess);
            Assert.True(tags[1].IsSuccess);
            Assert.False(tags[2].IsSuccess);
            Assert.Equal(existingTag0.Id, tags[0].Entity!.Id);
            Assert.Equal(existingTag1.Id, tags[1].Entity!.Id);
            Assert.Null(tags[2].Entity);
        }
    }

    public class TheTagConversationAsyncMethod
    {
        [Fact]
        public async Task UpdatesConversationTags()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<TagRepository>();
            var tag0 = await repository.CreateAsync(
                new Tag { Name = "tag-0", Organization = env.TestData.Organization },
                env.TestData.User);
            var tag1 = await repository.CreateAsync(
                new Tag { Name = "tag-1", Organization = env.TestData.Organization },
                env.TestData.User);
            var aiTag = await repository.CreateAsync(
                new Tag { Name = "ai:generated:tag", Organization = env.TestData.Organization },
                env.TestData.User);
            var foreignTag = await repository.CreateAsync(
                new Tag { Name = "le-tag", Organization = env.TestData.ForeignOrganization },
                env.TestData.ForeignUser);
            Assert.True(aiTag.Generated);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);

            await repository.TagConversationAsync(conversation, new[] { tag0.Id, tag1.Id, aiTag.Id, foreignTag.Id }, env.TestData.User);

            await env.ReloadAsync(conversation);
            Assert.Equal(
                new[] { "tag-0", "tag-1", "ai:generated:tag" },
                conversation.Tags
                    .OrderBy(t => t.TagId)
                    .Select(t => t.Tag.Name)
                    .ToArray());
        }

        [Fact]
        public async Task UpdatesConversationTagsButDoesNotRemoveSystemTags()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<TagRepository>();
            var tag0 = await repository.CreateAsync(
                new Tag { Name = "tag-0", Organization = env.TestData.Organization },
                env.TestData.User);
            var tag1 = await repository.CreateAsync(
                new Tag { Name = "tag-1", Organization = env.TestData.Organization },
                env.TestData.User);
            var aiTag = await repository.CreateAsync(
                new Tag { Name = "ai:generated:tag", Organization = env.TestData.Organization },
                env.TestData.User);
            var foreignTag = await repository.CreateAsync(
                new Tag { Name = "le-tag", Organization = env.TestData.ForeignOrganization },
                env.TestData.ForeignUser);
            Assert.True(aiTag.Generated);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            await repository.TagConversationAsync(conversation, new[] { tag0.Id, tag1.Id, aiTag.Id, foreignTag.Id }, env.TestData.User);

            await repository.TagConversationAsync(conversation, new[] { tag0.Id, tag1.Id, foreignTag.Id }, env.TestData.User);

            await env.ReloadAsync(conversation);
            Assert.Equal(
                new[] { "tag-0", "tag-1", "ai:generated:tag" },
                conversation.Tags
                    .OrderBy(t => t.TagId)
                    .Select(t => t.Tag.Name)
                    .ToArray());

            await repository.TagConversationAsync(conversation, new[] { tag1.Id, foreignTag.Id }, env.TestData.User);

            await env.ReloadAsync(conversation);
            Assert.Equal(
                new[] { "tag-1", "ai:generated:tag" },
                conversation.Tags
                    .OrderBy(t => t.TagId)
                    .Select(t => t.Tag.Name)
                    .ToArray());
        }
    }

    public class TheEnsureTagsAsyncMethod
    {
        [Fact]
        public async Task CreatesMissingTagsAndReturnsExisting()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<TagRepository>();
            var existingTag = await repository.CreateAsync(
                new Tag { Name = "tag0", Organization = env.TestData.Organization },
                env.TestData.User);
            var anotherExistingTag = await repository.CreateAsync(
                new Tag { Name = "tag-1", Organization = env.TestData.Organization },
                env.TestData.User);

            var tags = await repository.EnsureTagsAsync(
                new[] { "tag0", "tag1", "tag2" },
                "AI generated tag",
                env.TestData.Member,
                env.TestData.Organization);

            Assert.Collection(tags.OrderBy(t => t.Name),
                t => Assert.Equal(existingTag.Id, t.Id),
                t => {
                    Assert.Equal("tag1", t.Name);
                    Assert.Equal("AI generated tag", t.Description);
                },
                t => {
                    Assert.Equal("tag2", t.Name);
                    Assert.Equal("AI generated tag", t.Description);
                }
            );
            var allTags = await repository.GetAllAsync(env.TestData.Organization);
            Assert.Collection(allTags.OrderBy(t => t.Name),
                t => Assert.Equal(anotherExistingTag.Id, t.Id),
                t => Assert.Equal(existingTag.Id, t.Id),
                t => {
                    Assert.Equal("tag1", t.Name);
                    Assert.Equal("AI generated tag", t.Description);
                },
                t => {
                    Assert.Equal("tag2", t.Name);
                    Assert.Equal("AI generated tag", t.Description);
                }
            );
        }
    }
}
