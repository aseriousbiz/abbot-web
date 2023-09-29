using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using NodaTime;
using Serious;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Functions.Runtime;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.Abbot.Serialization;
using Serious.Slack;
using Serious.TestHelpers;
using Xunit;

public class RoomsClientTests
{
    public class TheCreateAsyncMethod
    {
        [Fact]
        public async Task PostsToCreateRoomEndpoint()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/rooms");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Put, new ConversationInfoResponse
            {
                Ok = true,
                Body = new ConversationInfo
                {
                    Id = "id1",
                    Name = "the-room",
                    Topic = new TopicInfo { Value = "the topic" },
                    Purpose = new TopicInfo { Value = "for talking about stuff" }
                }
            });
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor());

            var result = await roomsClient.CreateAsync("the-room", true);

            Assert.NotNull(result);
            Assert.NotNull(result.Value);
            Assert.True(result.Ok);
            Assert.Equal("the-room", result.Value.Name);
            Assert.Equal("the topic", result.Value.Topic);
            Assert.Equal("for talking about stuff", result.Value.Purpose);
            var body = apiClient.SentJson[(expectedUrl, HttpMethod.Put)][0];
            var json = AbbotJsonFormat.Default.Serialize(body, writeIndented: false);
            Assert.Equal("{\"name\":\"the-room\",\"is_private\":true}", json);
        }

        [Fact]
        public async Task ReportsErrorFromApi()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/rooms");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Put, new ConversationInfoResponse
            {
                Ok = false,
                Error = "it broke"
            });
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor());

            var result = await roomsClient.CreateAsync("the-room", true);

            Assert.NotNull(result);
            Assert.Null(result.Value);
            Assert.False(result.Ok);
            Assert.Equal("it broke", result.Error);
        }

        [Fact]
        public async Task ReportsErrorWhenRequestFails()
        {
            var apiClient = new FakeSkillApiClient(42);
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor());

            var result = await roomsClient.CreateAsync("the-room", true);

            Assert.NotNull(result);
            Assert.Null(result.Value);
            Assert.False(result.Ok);
            Assert.Equal("unknown error occurred", result.Error);
        }
    }

    public class TheArchiveAsyncMethod
    {
        [Fact]
        public async Task PostsToArchiveRoomEndpoint()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/23/rooms/C01234%2FABC/archive");
            var apiClient = new FakeSkillApiClient(23);
            apiClient.AddResponse(expectedUrl, HttpMethod.Put, new ApiResponse
            {
                Ok = true
            });
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor());

            var result = await roomsClient.ArchiveAsync(new PlatformRoom("C01234/ABC", "the-room"));

            Assert.NotNull(result);
            Assert.True(result.Ok);
        }
    }

    public class TheInviteUsersAsyncMethod
    {
        [Fact]
        public async Task SendsPostRequestWithRoomIds()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/rooms/C01234%2FABC");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Post, new ApiResponse
            {
                Ok = true
            });
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor());
            var room = new PlatformRoom("C01234/ABC", "the-room");
            var invitees = new[]
            {
                new FakeChatUser("U123", "fresh-prince", "Will Smith"),
                new FakeChatUser("U124", "jada", "Jada Pinkett Smith")
            };

            var result = await roomsClient.InviteUsersAsync(room, invitees);

            Assert.NotNull(result);
            Assert.True(result.Ok);
            var body = apiClient.SentJson[(expectedUrl, HttpMethod.Post)][0] as IEnumerable<string>;
            Assert.NotNull(body);
            var userIds = body.ToReadOnlyList();
            Assert.Equal("U123", userIds[0]);
            Assert.Equal("U124", userIds[1]);
        }
    }

    public class TheSetTopicAsyncMethod
    {
        [Fact]
        public async Task SendsPostRequestWithNewTopic()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/rooms/C01234%2FABC/topic");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Post, new ApiResponse
            {
                Ok = true
            });
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor());
            var room = new PlatformRoom("C01234/ABC", "the-room");

            var result = await roomsClient.SetTopicAsync(room, "butterflies and rainbows");

            Assert.NotNull(result);
            Assert.True(result.Ok);
            var body = apiClient.SentJson[(expectedUrl, HttpMethod.Post)][0] as string;
            Assert.NotNull(body);
            Assert.Equal("butterflies and rainbows", body);
        }
    }

    public class TheSetPurposeAsyncMethod
    {
        [Fact]
        public async Task SendsPostRequestWithNewPurpose()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/rooms/C01234%2FABC/purpose");
            var apiClient = new FakeSkillApiClient(42);
            apiClient.AddResponse(expectedUrl, HttpMethod.Post, new ApiResponse
            {
                Ok = true
            });
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor());
            var room = new PlatformRoom("C01234/ABC", "the-room");

            var result = await roomsClient.SetPurposeAsync(room, "to rule the world");

            Assert.NotNull(result);
            Assert.True(result.Ok);
            var body = apiClient.SentJson[(expectedUrl, HttpMethod.Post)][0] as string;
            Assert.NotNull(body);
            Assert.Equal("to rule the world", body);
        }
    }

    public class TheGetConversationMethod
    {
        [Fact]
        public void ReturnsRoomConversationFromProvidedId()
        {
            var client = new RoomsClient(new FakeSkillApiClient(42), new FakeSkillContextAccessor());
            var room = client.GetTarget("C12345");

            Assert.Equal("C12345", room.Id);
            Assert.Equal(new ChatAddress(ChatAddressType.Room, "C12345"), room.Address);
            Assert.Equal(new ChatAddress(ChatAddressType.Room, "C12345", "T1"), room.GetThread("T1").Address);
        }
    }

    public class TheGetCoverageAsyncMethod
    {
        [Fact]
        public async Task UsesCallersTimeZoneWhenNull()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/rooms/id1/coverage/FirstResponder?tz=America%2FLos_Angeles");
            var apiClient = new FakeSkillApiClient(42);
            var expectedResult = new List<WorkingHours>
            {
                new(new(9), new(5))
            };
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, expectedResult);
            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Los_Angeles");
            var skillInfo = new SkillInfo { From = new PlatformUser { Location = new Location { TimeZone = tz } } };
            var message = new SkillMessage { RunnerInfo = new SkillRunnerInfo { SkillId = 42 }, SkillInfo = skillInfo };
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor
            {
                SkillContext = new SkillContext(message, "api")
            });

            var result = await roomsClient.GetCoverageAsync(new RoomMessageTarget("id1"), RoomRole.FirstResponder, null);

            Assert.NotNull(result);
            Assert.NotNull(result.Value);
            Assert.True(result.Ok);
            var hours = Assert.Single(result.Value);
            Assert.Equal(expectedResult.Single(), hours);
        }

        [Fact]
        public async Task UsesSpecifiedTimeZoneWhenNotNull()
        {
            var expectedUrl = new Uri("https://ab.bot/api/skills/42/rooms/id1/coverage/FirstResponder?tz=America%2FNew_York");
            var apiClient = new FakeSkillApiClient(42);
            var expectedResult = new List<WorkingHours>
            {
                new(new(9), new(5))
            };
            apiClient.AddResponse(expectedUrl, HttpMethod.Get, expectedResult);
            var tz = DateTimeZoneProviders.Tzdb.GetZoneOrNull("America/Phoenix");
            var skillInfo = new SkillInfo { From = new PlatformUser { Location = new Location { TimeZone = tz } } };
            var message = new SkillMessage { RunnerInfo = new SkillRunnerInfo { SkillId = 42 }, SkillInfo = skillInfo };
            var roomsClient = new RoomsClient(apiClient, new FakeSkillContextAccessor
            {
                SkillContext = new SkillContext(message, "api")
            });

            var result = await roomsClient.GetCoverageAsync(new RoomMessageTarget("id1"), RoomRole.FirstResponder, "America/New_York");

            Assert.NotNull(result);
            Assert.NotNull(result.Value);
            Assert.True(result.Ok);
            var hours = Assert.Single(result.Value);
            Assert.Equal(expectedResult.Single(), hours);
        }
    }
}
