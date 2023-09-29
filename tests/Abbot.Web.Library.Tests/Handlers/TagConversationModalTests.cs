using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.BlockKit.LayoutBlocks;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

public class TagConversationModalTests
{
    public class TheOnInteractionAsyncMethod
    {
        [Fact]
        public async Task ShowsConversationUserTags()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<TagRepository>();
            var tag0 = await repository.CreateAsync(
                new Tag { Name = "tag-0", Organization = env.TestData.Organization },
                env.TestData.User);
            var tag1 = await repository.CreateAsync(
                new Tag { Name = "tag-1", Organization = env.TestData.Organization },
                env.TestData.User);
            var tag2 = await repository.CreateAsync(
                new Tag { Name = "tag-2", Organization = env.TestData.Organization },
                env.TestData.User);
            var aiTag = await repository.CreateAsync(
                new Tag { Name = "ai:generated:tag", Organization = env.TestData.Organization },
                env.TestData.User);
            Assert.True(aiTag.Generated);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            await repository.TagConversationAsync(conversation, new[] { tag0.Id, tag1.Id, aiTag.Id }, env.TestData.User);
            var platformEvent = env.CreateFakePlatformEvent(new ViewBlockActionsPayload
            {
                TriggerId = "trigger-id",
                Actions = new[]
                {
                    new ButtonElement
                    {
                        ActionId = "some-action",
                        Value = $"{conversation.Id}",
                    },
                },
                View = new ModalView
                {
                    Id = "view-id",
                },
            });
            var modal = env.Activate<TagConversationModal>();
            var context = new ViewContext<ViewBlockActionsPayload>(platformEvent, modal);

            await modal.OnInteractionAsync(context);

            var (_, pushedModal) = env.Responder.PushedModals.Single(m => m.Item1 == "trigger-id");
            var selectMenu = pushedModal.Blocks.FindInputElementByBlockId<MultiStaticSelectMenu>(
                nameof(TagConversationModal.SubmissionState.Tags));
            Assert.NotNull(selectMenu?.InitialOptions);
            var initialIds = selectMenu.InitialOptions.Select(o => o.Value).ToArray();
            var expectedInitialIds = new[] { $"{tag0.Id}", $"{tag1.Id}" };
            Assert.Equal(expectedInitialIds, initialIds);
            Assert.NotNull(selectMenu.Options);
            var optionIds = selectMenu.Options.Select(o => o.Value).ToArray();
            var expectedOptionIds = new[] { $"{tag0.Id}", $"{tag1.Id}", $"{tag2.Id}" };
            Assert.Equal(expectedOptionIds, optionIds);
        }
    }

    public class TheOnSubmissionAsyncMethod
    {
        [Fact]
        public async Task UpdatesConversationTagsWithoutTouchingGeneratedTags()
        {
            var env = TestEnvironment.Create();
            var repository = env.Activate<TagRepository>();
            var tag0 = await repository.CreateAsync(
                new Tag { Name = "tag-0", Organization = env.TestData.Organization },
                env.TestData.User);
            var tag1 = await repository.CreateAsync(
                new Tag { Name = "tag-1", Organization = env.TestData.Organization },
                env.TestData.User);
            var tag2 = await repository.CreateAsync(
                new Tag { Name = "tag-2", Organization = env.TestData.Organization },
                env.TestData.User);
            var aiTag = await repository.CreateAsync(
                new Tag { Name = "ai:generated:tag", Organization = env.TestData.Organization },
                env.TestData.User);
            Assert.True(aiTag.Generated);
            var room = await env.CreateRoomAsync(managedConversationsEnabled: true);
            var conversation = await env.CreateConversationAsync(room);
            await repository.TagConversationAsync(conversation, new[] { tag0.Id, tag1.Id, aiTag.Id }, env.TestData.User);
            var viewSubmissionPayload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    PrivateMetadata = $"{conversation.Id}",
                    State = new BlockActionsState(new Dictionary<string, Dictionary<string, IPayloadElement>>
                    {
                        [nameof(TagConversationModal.SubmissionState.Tags)] = new()
                        {
                            ["add-tags"] = new MultiStaticSelectMenu
                            {
                                SelectedOptions = new Option[]
                                {
                                    new("whatev1", $"{tag1.Id}"),
                                    new("whatev2", $"{tag2.Id}"),
                                }
                            }
                        }
                    })
                }
            };
            var modal = env.Activate<TagConversationModal>();
            var viewContext = env.CreateFakeViewContext(viewSubmissionPayload, modal);

            await modal.OnSubmissionAsync(viewContext);

            await env.ReloadAsync(conversation);
            var expectedTagIds = new[] { tag1.Id, tag2.Id, aiTag.Id };
            var tagIds = conversation.Tags.Select(t => t.Tag.Id).OrderBy(t => t).ToArray();
            Assert.Equal(expectedTagIds, tagIds);
        }
    }
}
