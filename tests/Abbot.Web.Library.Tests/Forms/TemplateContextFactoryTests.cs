using System.Collections.Generic;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Forms;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Xunit;

public class TemplateContextFactoryTests
{
    public class TheCreateTicketTemplateContextAsyncMethod
    {
        [Fact]
        public async Task PopulatesSimpleProperties()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IMessageRenderer, MessageRenderer>()
                .Build();
            var org = env.TestData.Organization;
            var actor = env.TestData.Member;
            await env.CreateMemberAsync();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room);
            var factory = env.Activate<TemplateContextFactory>();

            var context = await factory.CreateTicketTemplateContextAsync(conversation, actor);

            Assert.Equal(
                new(conversation.Id,
                    $"https://app.ab.bot/conversations/{conversation.Id}",
                    conversation.GetFirstMessageUrl().ToString(),
                    conversation.Title,
                    conversation.Title,
                    conversation.LastMessagePostedOn,
                    ExpectedMemberModel(conversation.StartedBy),
                    conversation.State),
                context.Conversation);

            var (roomId, name, platformRoomId, conversationEnabled, metadata, roomType) = context.Room;
            Assert.Empty(metadata);
            Assert.Equal(
                (room.Id, room.Name, room.PlatformRoomId, room.ManagedConversationsEnabled, room.RoomType),
                (roomId, name, platformRoomId, conversationEnabled, roomType));

            Assert.Equal(
                new(org.Id,
                    org.Name,
                    org.Domain,
                    org.Avatar,
                    org.PlatformType,
                    org.PlatformId),
                context.Organization);

            Assert.Equal(ExpectedMemberModel(actor), context.Actor);

            MemberTemplateModel ExpectedMemberModel(Member expected) =>
                new(expected.Id,
                    expected.Active,
                    expected.DisplayName,
                    expected.User.Email,
                    expected.FormattedAddress,
                    expected.TimeZoneId,
                    expected.User.PlatformUserId,
                    SlackFormatter.UserUrl(
                        expected.Organization.Domain,
                        expected.User.PlatformUserId).ToString(),
                    expected.User.Avatar);
        }

        [Fact]
        public async Task PopulatesRoomMetadata()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IMessageRenderer, MessageRenderer>()
                .Build();
            var org = env.TestData.Organization;
            var actor = env.TestData.Member;
            await env.CreateMemberAsync();
            var room = await env.CreateRoomAsync();
            await env.Metadata.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "custom_field_1", Organization = org }, actor.User);
            await env.Metadata.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "custom_field_2", Organization = org }, actor.User);
            var metadataValues = new Dictionary<string, string?>
            {
                ["custom_field_1"] = "custom_value_1",
                ["custom_field_2"] = "custom_value_2",
            };
            await env.Metadata.UpdateRoomMetadataAsync(room, metadataValues, actor);
            var conversation = await env.CreateConversationAsync(room);
            var factory = env.Activate<TemplateContextFactory>();

            var context = await factory.CreateTicketTemplateContextAsync(conversation, actor);

            var (roomId, name, platformRoomId, conversationEnabled, metadata, roomType) = context.Room;
            Assert.Collection(metadata,
                k => Assert.Equal(("custom_field_1", "custom_value_1"), (k.Key, k.Value)),
                k => Assert.Equal(("custom_field_2", "custom_value_2"), (k.Key, k.Value)));
            Assert.Equal(
                (room.Id, room.Name, room.PlatformRoomId, room.ManagedConversationsEnabled, room.RoomType),
                (roomId, name, platformRoomId, conversationEnabled, roomType));
        }

        [Fact]
        public async Task StripsMrkdwnForConversationTitlePlainTextProperty()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ReplaceService<IMessageRenderer, MessageRenderer>()
                .Build();
            var actor = env.TestData.Member;
            var mentioned = await env.CreateMemberAsync();
            var room = await env.CreateRoomAsync();
            var conversation = await env.CreateConversationAsync(room, title: $"This is a title with a {mentioned.ToMention()} user and {room.ToMention()} room.");
            var factory = env.Activate<TemplateContextFactory>();

            var context = await factory.CreateTicketTemplateContextAsync(conversation, actor);

            Assert.Equal(
                $"This is a title with a {mentioned.ToMention()} user and {room.ToMention()} room.",
                context.Conversation.Title);
            Assert.Equal(
                $"This is a title with a {mentioned.User.DisplayName} user and #{room.Name} room.",
                context.Conversation.PlainTextTitle);
        }
    }
}
